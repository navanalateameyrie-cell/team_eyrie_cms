using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/fee-schedules")]
public class FeeSchedulesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public FeeSchedulesController(CmsDbContext db) => _db = db;

    public record FeeScheduleDto(
        Guid Id, Guid ServiceDefinitionId, string ServiceCode, string ServiceName,
        Guid? ClientId, string? ClientCode, string? ClientName,
        decimal AmountInr, decimal GstRatePercent,
        DateOnly EffectiveFrom, DateOnly? EffectiveTo,
        bool IsActive, string? Notes, int Version);

    public record FeeScheduleUpsertRequest(
        [Required] Guid ServiceDefinitionId,
        Guid? ClientId,
        [Range(0, 10_000_000)] decimal AmountInr,
        [Range(0, 100)] decimal GstRatePercent,
        [Required] DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        [StringLength(2000)] string? Notes);

    [HttpGet]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<FeeScheduleDto>>> List(
        [FromQuery] Guid? serviceId, [FromQuery] Guid? clientId,
        [FromQuery] bool? activeOnly = true, CancellationToken ct = default)
    {
        var q = _db.FeeSchedules.AsNoTracking().AsQueryable();
        if (serviceId is not null) q = q.Where(f => f.ServiceDefinitionId == serviceId);
        if (clientId is not null) q = q.Where(f => f.ClientId == clientId);
        if (activeOnly == true) q = q.Where(f => f.IsActive);

        var rows = await (from f in q
                          join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                          join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id into cj
                          from c in cj.DefaultIfEmpty()
                          orderby s.Code, f.EffectiveFrom descending
                          select new { f, s, c }).ToListAsync(ct);
        return rows.Select(r => new FeeScheduleDto(
            r.f.Id, r.s.Id, r.s.Code, r.s.Name,
            r.c == null ? null : r.c.Id, r.c == null ? null : r.c.Code, r.c == null ? null : r.c.LegalName,
            r.f.AmountInr, r.f.GstRatePercent,
            r.f.EffectiveFrom, r.f.EffectiveTo,
            r.f.IsActive, r.f.Notes, r.f.Version)).ToList();
    }

    [HttpPost]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<FeeScheduleDto>> Create([FromBody] FeeScheduleUpsertRequest req, CancellationToken ct)
    {
        if (!await _db.ServiceDefinitions.AnyAsync(s => s.Id == req.ServiceDefinitionId, ct))
            return UnprocessableEntity(new { error = "unknown_service" });
        if (req.ClientId is Guid cid && cid != Guid.Empty &&
            !await _db.Clients.AnyAsync(c => c.Id == cid, ct))
            return UnprocessableEntity(new { error = "unknown_client" });

        var fee = new FeeSchedule(req.ServiceDefinitionId, req.ClientId,
            req.AmountInr, req.GstRatePercent, req.EffectiveFrom, req.EffectiveTo, req.Notes);
        _db.FeeSchedules.Add(fee);
        await _db.SaveChangesAsync(ct);
        return (await List(req.ServiceDefinitionId, req.ClientId, activeOnly: false, ct)).Value!
            .First(f => f.Id == fee.Id);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<FeeScheduleDto>> Update(Guid id, [FromBody] FeeScheduleUpsertRequest req, CancellationToken ct)
    {
        var fee = await _db.FeeSchedules.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fee is null) return NotFound();
        fee.UpdateProfile(req.AmountInr, req.GstRatePercent, req.EffectiveFrom, req.EffectiveTo, req.Notes);
        await _db.SaveChangesAsync(ct);
        return (await List(fee.ServiceDefinitionId, fee.ClientId, activeOnly: false, ct)).Value!
            .First(f => f.Id == fee.Id);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var fee = await _db.FeeSchedules.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fee is null) return NotFound();
        fee.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Resolve the fee for a (service, client, as-of date). Client override wins, then service default.</summary>
    [HttpGet("resolve")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<FeeScheduleDto?>> Resolve(
        [FromQuery] Guid serviceId, [FromQuery] Guid clientId, [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var candidates = await (from f in _db.FeeSchedules.AsNoTracking()
                                where f.ServiceDefinitionId == serviceId
                                      && f.IsActive
                                      && f.EffectiveFrom <= today
                                      && (f.EffectiveTo == null || f.EffectiveTo >= today)
                                      && (f.ClientId == null || f.ClientId == clientId)
                                join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                                join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id into cj
                                from c in cj.DefaultIfEmpty()
                                select new { f, s, c }).ToListAsync(ct);
        // Client-specific wins over service default; among same specificity, latest EffectiveFrom.
        var winner = candidates
            .OrderByDescending(r => r.f.ClientId.HasValue)
            .ThenByDescending(r => r.f.EffectiveFrom)
            .FirstOrDefault();
        if (winner is null) return (FeeScheduleDto?)null;
        return new FeeScheduleDto(winner.f.Id, winner.s.Id, winner.s.Code, winner.s.Name,
            winner.c == null ? null : winner.c.Id, winner.c == null ? null : winner.c.Code, winner.c == null ? null : winner.c.LegalName,
            winner.f.AmountInr, winner.f.GstRatePercent,
            winner.f.EffectiveFrom, winner.f.EffectiveTo,
            winner.f.IsActive, winner.f.Notes, winner.f.Version);
    }
}
