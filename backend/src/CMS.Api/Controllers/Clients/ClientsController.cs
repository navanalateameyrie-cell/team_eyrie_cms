using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Application.Clients;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients")]
public class ClientsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IClientCodeGenerator _codes;

    public ClientsController(CmsDbContext db, IClientCodeGenerator codes)
    {
        _db = db;
        _codes = codes;
    }

    public record ClientDto(Guid Id, string Code, string? LegacyClientId, string LegalName, string? TradeName,
                            string? Pan, Guid EntityTypeId, string? EntityTypeName,
                            Guid? ClientGroupId, string? ClientGroupName,
                            bool IsActive, int Version);

    public record ClientSummaryDto(Guid Id, string Code, string LegalName, string? TradeName, string? Pan, bool IsActive);

    public record ClientListResponse(int Total, int Skip, int Take, IReadOnlyList<ClientSummaryDto> Items);

    public record DuplicatePanCandidate(Guid Id, string Code, string LegalName, bool IsActive);

    public record CreateClientRequest(
        [Required, StringLength(300)] string LegalName,
        [Required] Guid EntityTypeId,
        [StringLength(300)] string? TradeName,
        [StringLength(10, MinimumLength = 10)] string? Pan,
        Guid? ClientGroupId,
        [StringLength(64)] string? LegacyClientId,
        bool AcknowledgeDuplicatePan = false);

    public record UpdateClientRequest(
        [Required, StringLength(300)] string LegalName,
        [Required] Guid EntityTypeId,
        [StringLength(300)] string? TradeName,
        [StringLength(10, MinimumLength = 10)] string? Pan,
        Guid? ClientGroupId,
        [StringLength(64)] string? LegacyClientId,
        [Required] int Version,
        bool AcknowledgeDuplicatePan = false);

    private static ClientDto MapFull(Client c) => new(
        c.Id, c.Code, c.LegacyClientId, c.LegalName, c.TradeName, c.Pan,
        c.EntityTypeId, c.EntityType?.Name, c.ClientGroupId, c.ClientGroup?.Name,
        c.IsActive, c.Version);

    private static ClientSummaryDto MapSummary(Client c) =>
        new(c.Id, c.Code, c.LegalName, c.TradeName, c.Pan, c.IsActive);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<ClientListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] Guid? entityTypeId,
        [FromQuery] Guid? clientGroupId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var query = _db.Clients.AsNoTracking().AsQueryable();
        if (active is not null) query = query.Where(c => c.IsActive == active);
        if (entityTypeId is not null) query = query.Where(c => c.EntityTypeId == entityTypeId);
        if (clientGroupId is not null) query = query.Where(c => c.ClientGroupId == clientGroupId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            query = query.Where(c =>
                c.Code.Contains(s) ||
                c.LegalName.ToLower().Contains(sLower) ||
                (c.TradeName != null && c.TradeName.ToLower().Contains(sLower)) ||
                (c.Pan != null && c.Pan.Contains(s.ToUpper())) ||
                (c.LegacyClientId != null && c.LegacyClientId.ToLower().Contains(sLower)));
        }

        var total = await query.CountAsync(ct);
        var page = await query.OrderBy(c => c.Code).Skip(skip).Take(take).ToListAsync(ct);
        return new ClientListResponse(total, skip, take, page.Select(MapSummary).ToList());
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<ClientDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.Clients.AsNoTracking()
            .Include(c => c.EntityType)
            .Include(c => c.ClientGroup)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return row is null ? NotFound() : MapFull(row);
    }

    [HttpGet("by-code/{code}")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<ClientDto>> GetByCode(string code, CancellationToken ct)
    {
        var row = await _db.Clients.AsNoTracking()
            .Include(c => c.EntityType)
            .Include(c => c.ClientGroup)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
        return row is null ? NotFound() : MapFull(row);
    }

    [HttpGet("duplicate-pan-candidates")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<DuplicatePanCandidate>>> DuplicatePanCandidates(
        [FromQuery, Required] string pan, [FromQuery] Guid? excludeId, CancellationToken ct)
    {
        var normalized = PanValidator.Normalize(pan);
        if (!PanValidator.IsValid(normalized))
            return BadRequest(new { error = "invalid_pan" });

        var query = _db.Clients.AsNoTracking().Where(c => c.Pan == normalized);
        if (excludeId is not null) query = query.Where(c => c.Id != excludeId);
        var rows = await query.Select(c => new DuplicatePanCandidate(c.Id, c.Code, c.LegalName, c.IsActive)).ToListAsync(ct);
        return rows;
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ClientDto>> Create([FromBody] CreateClientRequest req, CancellationToken ct)
    {
        if (!await _db.EntityTypes.AnyAsync(e => e.Id == req.EntityTypeId && e.IsActive, ct))
            return BadRequest(new { error = "invalid_entity_type" });

        if (req.ClientGroupId is not null &&
            !await _db.ClientGroups.AnyAsync(g => g.Id == req.ClientGroupId && g.IsActive, ct))
            return BadRequest(new { error = "invalid_client_group" });

        string? pan;
        try { pan = PanValidator.NormalizeOrThrow(req.Pan); }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_pan", detail = ex.Message }); }

        if (!string.IsNullOrEmpty(req.LegacyClientId) &&
            await _db.Clients.AnyAsync(c => c.LegacyClientId == req.LegacyClientId, ct))
            return Conflict(new { error = "duplicate_legacy_id" });

        if (pan is not null && !req.AcknowledgeDuplicatePan)
        {
            var candidates = await _db.Clients.AsNoTracking()
                .Where(c => c.Pan == pan)
                .Select(c => new DuplicatePanCandidate(c.Id, c.Code, c.LegalName, c.IsActive))
                .ToListAsync(ct);
            if (candidates.Count > 0)
                return UnprocessableEntity(new
                {
                    error = "duplicate_pan_requires_acknowledgement",
                    pan,
                    candidates
                });
        }

        var code = await _codes.NextAsync(ct);
        var client = new Client(code, req.LegalName, req.EntityTypeId, req.TradeName, pan, req.ClientGroupId, req.LegacyClientId);
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(client).Reference(c => c.EntityType).LoadAsync(ct);
        if (client.ClientGroupId is not null)
            await _db.Entry(client).Reference(c => c.ClientGroup).LoadAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = client.Id, version = "1.0" }, MapFull(client));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ClientDto>> Update(Guid id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();

        if (!await _db.EntityTypes.AnyAsync(e => e.Id == req.EntityTypeId && e.IsActive, ct))
            return BadRequest(new { error = "invalid_entity_type" });
        if (req.ClientGroupId is not null &&
            !await _db.ClientGroups.AnyAsync(g => g.Id == req.ClientGroupId && g.IsActive, ct))
            return BadRequest(new { error = "invalid_client_group" });

        string? pan;
        try { pan = PanValidator.NormalizeOrThrow(req.Pan); }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_pan", detail = ex.Message }); }

        if (!string.IsNullOrEmpty(req.LegacyClientId) &&
            await _db.Clients.AnyAsync(c => c.LegacyClientId == req.LegacyClientId && c.Id != id, ct))
            return Conflict(new { error = "duplicate_legacy_id" });

        if (pan is not null && pan != client.Pan && !req.AcknowledgeDuplicatePan)
        {
            var candidates = await _db.Clients.AsNoTracking()
                .Where(c => c.Pan == pan && c.Id != id)
                .Select(c => new DuplicatePanCandidate(c.Id, c.Code, c.LegalName, c.IsActive))
                .ToListAsync(ct);
            if (candidates.Count > 0)
                return UnprocessableEntity(new
                {
                    error = "duplicate_pan_requires_acknowledgement",
                    pan,
                    candidates
                });
        }

        // Enforce caller-supplied version for optimistic concurrency.
        _db.Entry(client).Property(c => c.Version).OriginalValue = req.Version;

        client.UpdateProfile(req.LegalName, req.TradeName, pan, req.EntityTypeId, req.ClientGroupId, req.LegacyClientId);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "concurrency_conflict" });
        }

        await _db.Entry(client).Reference(c => c.EntityType).LoadAsync(ct);
        if (client.ClientGroupId is not null)
            await _db.Entry(client).Reference(c => c.ClientGroup).LoadAsync(ct);

        return MapFull(client);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ClientDto>> Deactivate(Guid id, [FromQuery, Required] int version, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();
        _db.Entry(client).Property(c => c.Version).OriginalValue = version;
        client.Deactivate();
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return MapFull(client);
    }

    [HttpPost("{id:guid}/reactivate")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ClientDto>> Reactivate(Guid id, [FromQuery, Required] int version, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();
        _db.Entry(client).Property(c => c.Version).OriginalValue = version;
        client.Reactivate();
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return MapFull(client);  // Code is preserved — the entity's own invariant.
    }
}
