using System.ComponentModel.DataAnnotations;
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
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/branches/{branchId:guid}/tax-registrations")]
public class BranchTaxRegistrationsController : ControllerBase
{
    private readonly CmsDbContext _db;
    public BranchTaxRegistrationsController(CmsDbContext db) => _db = db;

    public record TaxRegistrationDto(Guid Id, Guid BranchId, TaxRegistrationType Type,
                                     string Number, DateOnly? EffectiveFrom, DateOnly? EffectiveTo,
                                     bool IsActive, int Version);

    public record UpsertRequest(
        [Required] TaxRegistrationType Type,
        [Required, StringLength(32)] string Number,
        DateOnly? EffectiveFrom,
        DateOnly? EffectiveTo);

    private static TaxRegistrationDto Map(TaxRegistration r) =>
        new(r.Id, r.BranchId, r.Type, r.Number, r.EffectiveFrom, r.EffectiveTo, r.IsActive, r.Version);

    private async Task<Branch?> BranchAsync(Guid clientId, Guid branchId, CancellationToken ct) =>
        await _db.Branches.FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == branchId, ct);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<TaxRegistrationDto>>> List(Guid clientId, Guid branchId, CancellationToken ct)
    {
        if (await BranchAsync(clientId, branchId, ct) is null) return NotFound();
        var rows = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => r.BranchId == branchId)
            .OrderBy(r => r.Type).ThenBy(r => r.Number)
            .ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<TaxRegistrationDto>> Create(Guid clientId, Guid branchId, [FromBody] UpsertRequest req, CancellationToken ct)
    {
        if (await BranchAsync(clientId, branchId, ct) is null) return NotFound();

        var (ok, err) = TaxRegistrationValidator.Validate(req.Type, req.Number);
        if (!ok) return BadRequest(new { error = err });

        var normalized = TaxRegistrationValidator.Normalize(req.Number);
        if (await _db.TaxRegistrations.AnyAsync(r =>
                r.BranchId == branchId && r.Type == req.Type && r.Number == normalized, ct))
            return Conflict(new { error = "duplicate_registration" });

        var row = new TaxRegistration(branchId, req.Type, normalized, req.EffectiveFrom, req.EffectiveTo);
        _db.TaxRegistrations.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { clientId, branchId, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<TaxRegistrationDto>> Update(Guid clientId, Guid branchId, Guid id,
        [FromBody] UpsertRequest req, [FromQuery, Required] int version, CancellationToken ct)
    {
        if (await BranchAsync(clientId, branchId, ct) is null) return NotFound();

        var row = await _db.TaxRegistrations.FirstOrDefaultAsync(r => r.BranchId == branchId && r.Id == id, ct);
        if (row is null) return NotFound();

        var (ok, err) = TaxRegistrationValidator.Validate(req.Type, req.Number);
        if (!ok) return BadRequest(new { error = err });

        var normalized = TaxRegistrationValidator.Normalize(req.Number);
        if ((req.Type != row.Type || normalized != row.Number) &&
            await _db.TaxRegistrations.AnyAsync(r =>
                r.BranchId == branchId && r.Type == req.Type && r.Number == normalized && r.Id != id, ct))
            return Conflict(new { error = "duplicate_registration" });

        try
        {
            _db.Entry(row).Property(r => r.Version).OriginalValue = version;
            row.Update(req.Type, normalized, req.EffectiveFrom, req.EffectiveTo);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }

        return Map(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<IActionResult> Deactivate(Guid clientId, Guid branchId, Guid id, [FromQuery, Required] int version, CancellationToken ct)
    {
        if (await BranchAsync(clientId, branchId, ct) is null) return NotFound();
        var row = await _db.TaxRegistrations.FirstOrDefaultAsync(r => r.BranchId == branchId && r.Id == id, ct);
        if (row is null) return NotFound();

        try
        {
            _db.Entry(row).Property(r => r.Version).OriginalValue = version;
            row.Deactivate();
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return NoContent();
    }
}
