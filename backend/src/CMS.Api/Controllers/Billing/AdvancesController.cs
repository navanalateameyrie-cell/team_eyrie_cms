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
[Route("api/v{version:apiVersion}/billing/advances")]
public class AdvancesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public AdvancesController(CmsDbContext db) => _db = db;

    public record AdvanceDto(Guid Id, Guid ClientId, string ClientName, DateOnly PaidOn,
        decimal AmountInr, decimal AmountAppliedInr, decimal AmountRemainingInr,
        PaymentMode Mode, string? Reference, string? Notes, bool IsReversed);

    public record CreateAdvanceRequest(
        [Required] Guid ClientId,
        [Required] DateOnly PaidOn,
        [Range(0.01, 10_000_000)] decimal AmountInr,
        [Required] PaymentMode Mode,
        [StringLength(100)] string? Reference,
        [StringLength(2000)] string? Notes);

    public record ApplyAdvanceRequest(
        [Required] Guid InvoiceId,
        [Range(0.01, 10_000_000)] decimal AmountInr);

    [HttpGet]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<AdvanceDto>>> List(
        [FromQuery] Guid? clientId, [FromQuery] bool? openOnly, CancellationToken ct)
    {
        var q = _db.Advances.AsNoTracking().AsQueryable();
        if (clientId is not null) q = q.Where(a => a.ClientId == clientId);
        if (openOnly == true) q = q.Where(a => !a.IsReversed && a.AmountAppliedInr < a.AmountInr);

        var rows = await (from a in q
                          join c in _db.Clients.AsNoTracking() on a.ClientId equals c.Id
                          orderby a.PaidOn descending
                          select new { a, c.LegalName }).ToListAsync(ct);
        return rows.Select(r => new AdvanceDto(r.a.Id, r.a.ClientId, r.LegalName,
            r.a.PaidOn, r.a.AmountInr, r.a.AmountAppliedInr,
            Math.Round(r.a.AmountInr - r.a.AmountAppliedInr, 2),
            r.a.Mode, r.a.Reference, r.a.Notes, r.a.IsReversed)).ToList();
    }

    [HttpPost]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<AdvanceDto>> Create([FromBody] CreateAdvanceRequest req, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct))
            return UnprocessableEntity(new { error = "unknown_client" });
        var adv = new Advance(req.ClientId, req.PaidOn, req.AmountInr, req.Mode, req.Reference, req.Notes);
        _db.Advances.Add(adv);
        await _db.SaveChangesAsync(ct);
        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == req.ClientId, ct);
        return CreatedAtAction(nameof(List), new { version = "1.0" },
            new AdvanceDto(adv.Id, adv.ClientId, client.LegalName, adv.PaidOn,
                adv.AmountInr, adv.AmountAppliedInr, adv.AmountRemainingInr,
                adv.Mode, adv.Reference, adv.Notes, adv.IsReversed));
    }

    [HttpPost("{id:guid}/apply")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<AdvanceDto>> Apply(Guid id, [FromBody] ApplyAdvanceRequest req, CancellationToken ct)
    {
        var adv = await _db.Advances.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (adv is null) return NotFound();
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == req.InvoiceId, ct);
        if (invoice is null) return UnprocessableEntity(new { error = "unknown_invoice" });
        if (invoice.ClientId != adv.ClientId)
            return UnprocessableEntity(new { error = "cross_client_apply_denied" });

        try
        {
            adv.Apply(req.AmountInr);
            invoice.RecordPayment(req.AmountInr);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_apply", detail = ex.Message }); }
        catch (ArgumentOutOfRangeException ex) { return UnprocessableEntity(new { error = "invalid_amount", detail = ex.Message }); }

        _db.AdvanceApplications.Add(new AdvanceApplication(adv.Id, invoice.Id, req.AmountInr));
        await _db.SaveChangesAsync(ct);
        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == adv.ClientId, ct);
        return new AdvanceDto(adv.Id, adv.ClientId, client.LegalName, adv.PaidOn,
            adv.AmountInr, adv.AmountAppliedInr, adv.AmountRemainingInr,
            adv.Mode, adv.Reference, adv.Notes, adv.IsReversed);
    }

    [HttpPost("applications/{applicationId:guid}/reverse")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<IActionResult> ReverseApplication(Guid applicationId, CancellationToken ct)
    {
        var app = await _db.AdvanceApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (app is null || app.IsReversed) return NotFound();
        var adv = await _db.Advances.FirstAsync(a => a.Id == app.AdvanceId, ct);
        var inv = await _db.Invoices.FirstAsync(i => i.Id == app.InvoiceId, ct);
        adv.ReverseApply(app.AmountInr);
        inv.ReversePayment(app.AmountInr);
        app.Reverse();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
