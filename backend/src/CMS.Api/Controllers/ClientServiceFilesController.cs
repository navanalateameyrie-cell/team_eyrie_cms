using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/service-files")]
public class ClientServiceFilesController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IServiceFileNumberGenerator _numbers;

    public ClientServiceFilesController(CmsDbContext db, IServiceFileNumberGenerator numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public record ServiceFileDto(
        Guid Id, string FileNumber, string? LegacyFileNumber,
        Guid ClientId, string? ClientCode, string? ClientLegalName, string? ClientPan,
        Guid? ClientGroupId, string? ClientGroupCode, string? ClientGroupName,
        Guid ServiceDefinitionId, string? ServiceCode, string? ServiceName,
        Guid? BranchId, string? BranchCode, string? BranchName,
        string? Gstin, string? Tan,
        ServiceFileStatus Status, DateOnly OpenedOn, DateOnly? ClosedOn,
        string? Notes, string? MetadataJson, int Version);

    public record CreateServiceFileRequest(
        [Required] Guid ServiceDefinitionId,
        Guid? BranchId,
        [Required] DateOnly OpenedOn,
        [StringLength(64)] string? LegacyFileNumber,
        [StringLength(2000)] string? Notes,
        string? MetadataJson = null);

    public record UpdateServiceFileRequest(
        [Required] Guid ServiceDefinitionId,
        Guid? BranchId,
        [StringLength(64)] string? LegacyFileNumber,
        [StringLength(2000)] string? Notes,
        [Required] int Version,
        string? MetadataJson = null);

    private static ServiceFileDto Map(ServiceFile f, string? gstin = null, string? tan = null) => new(
        f.Id, f.FileNumber, f.LegacyFileNumber,
        f.ClientId, f.Client?.Code, f.Client?.LegalName, f.Client?.Pan,
        f.Client?.ClientGroupId, f.Client?.ClientGroup?.Code, f.Client?.ClientGroup?.Name,
        f.ServiceDefinitionId, f.ServiceDefinition?.Code, f.ServiceDefinition?.Name,
        f.BranchId, f.Branch?.Code, f.Branch?.Name,
        gstin, tan,
        f.Status, f.OpenedOn, f.ClosedOn, f.Notes, f.MetadataJson, f.Version);

    private IQueryable<ServiceFile> WithIncludes() => _db.ServiceFiles
        .Include(f => f.Client).ThenInclude(c => c!.ClientGroup)
        .Include(f => f.ServiceDefinition).Include(f => f.Branch);

    private async Task<(string? gstin, string? tan)> ResolveBranchRegsAsync(Guid? branchId, CancellationToken ct)
    {
        if (branchId is null) return (null, null);
        var regs = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => r.BranchId == branchId && r.IsActive)
            .Select(r => new { r.Type, r.Number }).ToListAsync(ct);
        string? gstin = regs.FirstOrDefault(r => r.Type == TaxRegistrationType.Gstin)?.Number;
        string? tan   = regs.FirstOrDefault(r => r.Type == TaxRegistrationType.Tan)?.Number;
        return (gstin, tan);
    }

    private async Task<bool> ClientExistsAsync(Guid clientId, CancellationToken ct) =>
        await _db.Clients.AnyAsync(c => c.Id == clientId, ct);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<ServiceFileDto>>> List(
        Guid clientId,
        [FromQuery] ServiceFileStatus? status,
        [FromQuery] Guid? serviceDefinitionId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        if (!await ClientExistsAsync(clientId, ct)) return NotFound();
        var q = WithIncludes().AsNoTracking().Where(f => f.ClientId == clientId);
        if (status is not null) q = q.Where(f => f.Status == status);
        if (serviceDefinitionId is not null) q = q.Where(f => f.ServiceDefinitionId == serviceDefinitionId);
        if (branchId is not null) q = q.Where(f => f.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(f => f.FileNumber.Contains(s)
                             || (f.LegacyFileNumber != null && f.LegacyFileNumber.ToLower().Contains(sLower))
                             || (f.Notes != null && f.Notes.ToLower().Contains(sLower)));
        }
        var rows = await q.OrderByDescending(f => f.OpenedOn).ToListAsync(ct);
        // Resolve gstin/tan per unique branch in one shot.
        var branchIds = rows.Where(r => r.BranchId.HasValue).Select(r => r.BranchId!.Value).Distinct().ToArray();
        var regs = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => branchIds.Contains(r.BranchId) && r.IsActive)
            .Select(r => new { r.BranchId, r.Type, r.Number })
            .ToListAsync(ct);
        string? gst(Guid? b) => b is null ? null : regs.FirstOrDefault(r => r.BranchId == b && r.Type == TaxRegistrationType.Gstin)?.Number;
        string? tan(Guid? b) => b is null ? null : regs.FirstOrDefault(r => r.BranchId == b && r.Type == TaxRegistrationType.Tan)?.Number;
        return Ok(rows.Select(r => Map(r, gst(r.BranchId), tan(r.BranchId))).ToList());
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<ServiceFileDto>> Get(Guid clientId, Guid id, CancellationToken ct)
    {
        var row = await WithIncludes().AsNoTracking()
            .FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (row is null) return NotFound();
        var (gstin, tan) = await ResolveBranchRegsAsync(row.BranchId, ct);
        return Map(row, gstin, tan);
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> Create(Guid clientId,
        [FromBody] CreateServiceFileRequest req, CancellationToken ct)
    {
        if (!await ClientExistsAsync(clientId, ct)) return NotFound();

        var serviceOk = await _db.ServiceDefinitions
            .AnyAsync(s => s.Id == req.ServiceDefinitionId && s.IsActive, ct);
        if (!serviceOk) return BadRequest(new { error = "invalid_or_inactive_service" });

        if (req.BranchId is not null)
        {
            var branchBelongs = await _db.Branches
                .AnyAsync(b => b.Id == req.BranchId && b.ClientId == clientId, ct);
            if (!branchBelongs) return BadRequest(new { error = "branch_not_in_client" });
        }

        // Scoped uniqueness: a legacy file number can repeat across clients/services (real-world
        // CA data reuses legacy codes). Only the (client, service, legacyFileNumber) tuple is unique.
        if (!string.IsNullOrWhiteSpace(req.LegacyFileNumber) &&
            await _db.ServiceFiles.AnyAsync(f => f.ClientId == clientId
                && f.ServiceDefinitionId == req.ServiceDefinitionId
                && f.LegacyFileNumber == req.LegacyFileNumber, ct))
            return Conflict(new { error = "duplicate_legacy_file_number" });

        // Validate metadata JSON against the service's declared schema.
        var svc = await _db.ServiceDefinitions.AsNoTracking()
            .FirstAsync(s => s.Id == req.ServiceDefinitionId, ct);
        var metaErrors = CMS.Application.Clients.ServiceFileMetadataValidator.Validate(
            svc.RequiredFieldsJson, svc.OptionalFieldsJson, req.MetadataJson);
        if (metaErrors.Count > 0)
            return UnprocessableEntity(new { error = "metadata_validation_failed", fieldErrors = metaErrors });

        var fileNumber = await _numbers.NextAsync(ct);
        var file = new ServiceFile(fileNumber, clientId, req.ServiceDefinitionId,
                                   req.BranchId, req.OpenedOn, req.LegacyFileNumber, req.Notes);
        if (!string.IsNullOrWhiteSpace(req.MetadataJson)) file.SetMetadata(req.MetadataJson);
        _db.ServiceFiles.Add(file);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(file).Reference(f => f.Client).LoadAsync(ct);
        if (file.Client is not null)
            await _db.Entry(file.Client).Reference(c => c.ClientGroup).LoadAsync(ct);
        await _db.Entry(file).Reference(f => f.ServiceDefinition).LoadAsync(ct);
        if (file.BranchId is not null)
            await _db.Entry(file).Reference(f => f.Branch).LoadAsync(ct);
        var (gstinC, tanC) = await ResolveBranchRegsAsync(file.BranchId, ct);

        return CreatedAtAction(nameof(Get),
            new { clientId, id = file.Id, version = "1.0" }, Map(file, gstinC, tanC));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> Update(Guid clientId, Guid id,
        [FromBody] UpdateServiceFileRequest req, CancellationToken ct)
    {
        var file = await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (file is null) return NotFound();

        var serviceOk = await _db.ServiceDefinitions
            .AnyAsync(s => s.Id == req.ServiceDefinitionId && s.IsActive, ct);
        if (!serviceOk) return BadRequest(new { error = "invalid_or_inactive_service" });

        if (req.BranchId is not null)
        {
            var branchBelongs = await _db.Branches
                .AnyAsync(b => b.Id == req.BranchId && b.ClientId == clientId, ct);
            if (!branchBelongs) return BadRequest(new { error = "branch_not_in_client" });
        }

        if (!string.IsNullOrWhiteSpace(req.LegacyFileNumber) &&
            await _db.ServiceFiles.AnyAsync(f => f.ClientId == clientId
                && f.ServiceDefinitionId == req.ServiceDefinitionId
                && f.LegacyFileNumber == req.LegacyFileNumber && f.Id != id, ct))
            return Conflict(new { error = "duplicate_legacy_file_number" });

        // Validate metadata JSON against the (possibly new) service's schema.
        var svc = await _db.ServiceDefinitions.AsNoTracking()
            .FirstAsync(s => s.Id == req.ServiceDefinitionId, ct);
        var metaErrors = CMS.Application.Clients.ServiceFileMetadataValidator.Validate(
            svc.RequiredFieldsJson, svc.OptionalFieldsJson, req.MetadataJson);
        if (metaErrors.Count > 0)
            return UnprocessableEntity(new { error = "metadata_validation_failed", fieldErrors = metaErrors });

        try
        {
            _db.Entry(file).Property(f => f.Version).OriginalValue = req.Version;
            file.UpdateProfile(req.ServiceDefinitionId, req.BranchId, req.LegacyFileNumber, req.Notes, req.MetadataJson);
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "invalid_file_state", detail = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "concurrency_conflict" });
        }

        await _db.Entry(file).Reference(f => f.Client).LoadAsync(ct);
        if (file.Client is not null) await _db.Entry(file.Client).Reference(c => c.ClientGroup).LoadAsync(ct);
        await _db.Entry(file).Reference(f => f.ServiceDefinition).LoadAsync(ct);
        if (file.BranchId is not null) await _db.Entry(file).Reference(f => f.Branch).LoadAsync(ct);
        var (gstinU, tanU) = await ResolveBranchRegsAsync(file.BranchId, ct);
        return Map(file, gstinU, tanU);
    }

    [HttpPost("{id:guid}/close")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> Close(Guid clientId, Guid id,
        [FromQuery, Required] int version, [FromQuery] DateOnly? closedOn, CancellationToken ct)
    {
        var file = await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (file is null) return NotFound();
        try
        {
            _db.Entry(file).Property(f => f.Version).OriginalValue = version;
            file.Close(closedOn ?? DateOnly.FromDateTime(DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        // NOTE: deliberately does not touch Client.IsActive or Branch.IsActive — independent lifecycle.
        { var rr = await WithIncludes().AsNoTracking().FirstAsync(f => f.Id == id, ct); var (g, t) = await ResolveBranchRegsAsync(rr.BranchId, ct); return Map(rr, g, t); }
    }

    [HttpPost("{id:guid}/reopen")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> Reopen(Guid clientId, Guid id,
        [FromQuery, Required] int version, CancellationToken ct)
    {
        var file = await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (file is null) return NotFound();
        try
        {
            _db.Entry(file).Property(f => f.Version).OriginalValue = version;
            file.Reopen();
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        { var rr = await WithIncludes().AsNoTracking().FirstAsync(f => f.Id == id, ct); var (g, t) = await ResolveBranchRegsAsync(rr.BranchId, ct); return Map(rr, g, t); }
    }

    [HttpPost("{id:guid}/put-on-hold")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> PutOnHold(Guid clientId, Guid id,
        [FromQuery, Required] int version, CancellationToken ct)
    {
        var file = await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (file is null) return NotFound();
        try
        {
            _db.Entry(file).Property(f => f.Version).OriginalValue = version;
            file.PutOnHold();
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "invalid_transition", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        { var rr = await WithIncludes().AsNoTracking().FirstAsync(f => f.Id == id, ct); var (g, t) = await ResolveBranchRegsAsync(rr.BranchId, ct); return Map(rr, g, t); }
    }

    [HttpPost("{id:guid}/resume")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ServiceFileDto>> Resume(Guid clientId, Guid id,
        [FromQuery, Required] int version, CancellationToken ct)
    {
        var file = await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == id, ct);
        if (file is null) return NotFound();
        try
        {
            _db.Entry(file).Property(f => f.Version).OriginalValue = version;
            file.Resume();
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        { var rr = await WithIncludes().AsNoTracking().FirstAsync(f => f.Id == id, ct); var (g, t) = await ResolveBranchRegsAsync(rr.BranchId, ct); return Map(rr, g, t); }
    }
}
