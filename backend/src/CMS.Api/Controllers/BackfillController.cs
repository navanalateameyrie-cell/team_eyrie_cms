using System.Text.Json;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>
/// One-shot / repeat-safe backfill utilities. Separate from the import engine because they operate
/// against already-committed master data (not staging).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/backfill")]
public class BackfillController : ControllerBase
{
    private static readonly HashSet<string> StateScopedCodes = new(StringComparer.OrdinalIgnoreCase)
    { "GST", "AGST", "TDS", "PT", "ESIPF" };

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly CmsDbContext _db;
    public BackfillController(CmsDbContext db) => _db = db;

    public record BackfillResult(
        int FilesScanned, int FilesLinkedToBranch, int FilesStampedGstin, int FilesStampedTan,
        int FilesSkippedNoPrimaryBranch, int FilesSkippedAlreadyHasMetadata, bool DryRun);

    [HttpPost("service-file-registrations")]
    [HasPermission(Permissions.Imports.Write)]
    public async Task<ActionResult<BackfillResult>> ServiceFileRegistrations(
        [FromQuery] bool dryRun = false, CancellationToken ct = default)
    {
        // 1. Find candidate service files: state-scoped + no branch yet (or empty metadata).
        var stateScopedSvcIds = await _db.ServiceDefinitions.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);
        var svcIdSet = stateScopedSvcIds
            .Where(s => StateScopedCodes.Contains(s.Code))
            .Select(s => s.Id)
            .ToHashSet();
        if (svcIdSet.Count == 0)
            return new BackfillResult(0, 0, 0, 0, 0, 0, dryRun);

        var files = await _db.ServiceFiles
            .Where(f => svcIdSet.Contains(f.ServiceDefinitionId)
                        && f.Status != ServiceFileStatus.Closed
                        && f.BranchId == null)
            .ToListAsync(ct);

        if (files.Count == 0)
            return new BackfillResult(0, 0, 0, 0, 0, 0, dryRun);

        // 2. Resolve each client's primary active branch in a single query.
        var clientIds = files.Select(f => f.ClientId).Distinct().ToArray();
        var primaryBranchByClient = await _db.Branches.AsNoTracking()
            .Where(b => clientIds.Contains(b.ClientId) && b.IsPrimary && b.IsActive)
            .Select(b => new { b.ClientId, b.Id })
            .ToDictionaryAsync(x => x.ClientId, x => x.Id, ct);

        // 3. Load active GSTIN/TAN per branch in one query.
        var branchIds = primaryBranchByClient.Values.Distinct().ToArray();
        var regs = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => branchIds.Contains(r.BranchId) && r.IsActive)
            .Select(r => new { r.BranchId, r.Type, r.Number })
            .ToListAsync(ct);

        string? Lookup(Guid branchId, TaxRegistrationType t) =>
            regs.FirstOrDefault(r => r.BranchId == branchId && r.Type == t)?.Number;

        int linked = 0, gstinStamped = 0, tanStamped = 0,
            noPrimary = 0, alreadyMeta = 0;

        foreach (var f in files)
        {
            if (!primaryBranchByClient.TryGetValue(f.ClientId, out var branchId))
            {
                noPrimary++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(f.MetadataJson))
            {
                // Caller has already curated metadata — do not overwrite. Only link the branch.
                alreadyMeta++;
                if (!dryRun)
                    f.UpdateProfile(f.ServiceDefinitionId, branchId,
                        f.LegacyFileNumber, f.Notes, f.MetadataJson);
                linked++;
                continue;
            }

            var meta = new Dictionary<string, string>();
            var gstin = Lookup(branchId, TaxRegistrationType.Gstin);
            var tan = Lookup(branchId, TaxRegistrationType.Tan);
            if (gstin is not null) { meta["gstin"] = gstin; gstinStamped++; }
            if (tan is not null)   { meta["tan"]   = tan;   tanStamped++; }
            var metaJson = meta.Count > 0 ? JsonSerializer.Serialize(meta, JsonOpts) : null;

            if (!dryRun)
            {
                f.UpdateProfile(f.ServiceDefinitionId, branchId,
                    f.LegacyFileNumber, f.Notes, metaJson);
            }
            linked++;
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);

        return new BackfillResult(files.Count, linked, gstinStamped, tanStamped,
                                  noPrimary, alreadyMeta, dryRun);
    }
}
