using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Application.Clients;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/search")]
public class SearchController : ControllerBase
{
    private readonly CmsDbContext _db;
    public SearchController(CmsDbContext db) => _db = db;

    public enum HitKind { Client, ServiceFile, TaxRegistration }

    public record SearchHit(
        HitKind Kind,
        Guid Id,
        Guid? ClientId,
        string Code,
        string Label,
        string? Context,
        string MatchedOn);

    public record SearchResponse(int Total, int Skip, int Take, string Query, IReadOnlyList<SearchHit> Items);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] HitKind? kind,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 100) take = 25;
        if (skip < 0) skip = 0;

        if (string.IsNullOrWhiteSpace(q))
            return new SearchResponse(0, skip, take, "", Array.Empty<SearchHit>());

        var raw = q.Trim();
        var lower = raw.ToLowerInvariant();
        var upper = raw.ToUpperInvariant();

        var all = new List<SearchHit>();

        if (kind is null or HitKind.Client)
            all.AddRange(await MatchClientsAsync(raw, lower, upper, ct));
        if (kind is null or HitKind.ServiceFile)
            all.AddRange(await MatchServiceFilesAsync(raw, lower, ct));
        if (kind is null or HitKind.TaxRegistration)
            all.AddRange(await MatchTaxRegistrationsAsync(upper, ct));

        var total = all.Count;
        var page = all
            .DistinctBy(h => (h.Kind, h.Id))
            .OrderBy(h => h.Kind).ThenBy(h => h.Label)
            .Skip(skip).Take(take).ToList();

        return new SearchResponse(total, skip, take, raw, page);
    }

    private async Task<IReadOnlyList<SearchHit>> MatchClientsAsync(
        string raw, string lower, string upper, CancellationToken ct)
    {
        // Code: exact-match short-circuit.
        var exactCode = await _db.Clients.AsNoTracking()
            .Where(c => c.Code == raw)
            .Select(c => new SearchHit(
                HitKind.Client, c.Id, c.Id, c.Code, c.LegalName, c.TradeName, "code"))
            .ToListAsync(ct);
        if (exactCode.Count > 0) return exactCode;

        var pan = PanValidator.Normalize(raw);
        var pq = _db.Clients.AsNoTracking().AsQueryable();
        var panHits = pan is not null
            ? await pq.Where(c => c.Pan == pan)
                .Select(c => new SearchHit(
                    HitKind.Client, c.Id, c.Id, c.Code, c.LegalName,
                    $"PAN · {c.Pan}", "pan"))
                .ToListAsync(ct)
            : new List<SearchHit>();

        var textHits = await _db.Clients.AsNoTracking()
            .Where(c =>
                c.Code.Contains(raw) ||
                c.LegalName.ToLower().Contains(lower) ||
                (c.TradeName != null && c.TradeName.ToLower().Contains(lower)) ||
                (c.LegacyClientId != null && c.LegacyClientId.ToLower().Contains(lower)))
            .Take(50)
            .Select(c => new SearchHit(
                HitKind.Client, c.Id, c.Id, c.Code, c.LegalName,
                c.LegacyClientId != null ? $"legacy · {c.LegacyClientId}" : c.TradeName,
                c.Code.Contains(raw) ? "code"
                : c.LegacyClientId != null && c.LegacyClientId.ToLower().Contains(lower) ? "legacy_id"
                : c.LegalName.ToLower().Contains(lower) ? "legal_name"
                : "trade_name"))
            .ToListAsync(ct);

        return panHits.Concat(textHits).ToList();
    }

    private async Task<IReadOnlyList<SearchHit>> MatchServiceFilesAsync(string raw, string lower, CancellationToken ct)
    {
        // File number: exact-match short-circuit.
        var exact = await _db.ServiceFiles.AsNoTracking()
            .Where(f => f.FileNumber == raw)
            .Include(f => f.Client).Include(f => f.ServiceDefinition)
            .Select(f => new SearchHit(
                HitKind.ServiceFile, f.Id, f.ClientId, f.FileNumber,
                $"{f.ServiceDefinition!.Name} · {f.Client!.LegalName}",
                $"file · {f.FileNumber}", "file_number"))
            .ToListAsync(ct);
        if (exact.Count > 0) return exact;

        return await _db.ServiceFiles.AsNoTracking()
            .Where(f =>
                f.FileNumber.Contains(raw) ||
                (f.LegacyFileNumber != null && f.LegacyFileNumber.ToLower().Contains(lower)))
            .Include(f => f.Client).Include(f => f.ServiceDefinition)
            .Take(50)
            .Select(f => new SearchHit(
                HitKind.ServiceFile, f.Id, f.ClientId, f.FileNumber,
                $"{f.ServiceDefinition!.Name} · {f.Client!.LegalName}",
                f.LegacyFileNumber != null ? $"legacy file · {f.LegacyFileNumber}" : null,
                f.FileNumber.Contains(raw) ? "file_number" : "legacy_file_number"))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<SearchHit>> MatchTaxRegistrationsAsync(string upper, CancellationToken ct)
    {
        var hits = await (
            from tr in _db.TaxRegistrations.AsNoTracking()
            where tr.Number.Contains(upper)
            join br in _db.Branches.AsNoTracking() on tr.BranchId equals br.Id
            join c in _db.Clients.AsNoTracking() on br.ClientId equals c.Id
            select new SearchHit(
                HitKind.TaxRegistration, tr.Id, c.Id,
                tr.Number, c.LegalName,
                $"{tr.Type} · {br.Name}", tr.Type.ToString().ToLower()))
            .Take(50)
            .ToListAsync(ct);
        return hits;
    }
}
