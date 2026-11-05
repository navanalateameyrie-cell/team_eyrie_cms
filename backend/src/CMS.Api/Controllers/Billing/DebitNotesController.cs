using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/debit-notes")]
public class DebitNotesController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IInvoiceNumberGenerator _numberGen;
    public DebitNotesController(CmsDbContext db, IInvoiceNumberGenerator numberGen)
    { _db = db; _numberGen = numberGen; }

    public record DebitNoteDto(Guid Id, string Number, string FinancialYear,
        Guid InvoiceId, string InvoiceNumber, Guid ClientId, string ClientName,
        DateOnly IssueDate, string? Reason,
        decimal SubtotalInr, decimal CgstInr, decimal SgstInr, decimal IgstInr, decimal TotalInr,
        CreditNoteStatus Status);

    public record CreateDebitNoteRequest(
        [Required] Guid InvoiceId,
        [Required] DateOnly IssueDate,
        [StringLength(500)] string? Reason,
        [Range(0.01, 10_000_000)] decimal SubtotalInr,
        [Range(0, 100)] decimal GstRatePercent);

    [HttpGet]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<DebitNoteDto>>> List(
        [FromQuery] Guid? invoiceId, [FromQuery] Guid? clientId, CancellationToken ct)
    {
        var q = _db.DebitNotes.AsNoTracking().AsQueryable();
        if (invoiceId is not null) q = q.Where(x => x.InvoiceId == invoiceId);
        if (clientId is not null) q = q.Where(x => x.ClientId == clientId);
        return await (from n in q
                      join i in _db.Invoices.AsNoTracking() on n.InvoiceId equals i.Id
                      join c in _db.Clients.AsNoTracking() on n.ClientId equals c.Id
                      orderby n.CreatedAtUtc descending
                      select new DebitNoteDto(n.Id, n.Number, n.FinancialYear,
                          i.Id, i.Number, c.Id, c.LegalName,
                          n.IssueDate, n.Reason,
                          n.SubtotalInr, n.CgstInr, n.SgstInr, n.IgstInr, n.TotalInr,
                          n.Status)).ToListAsync(ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<DebitNoteDto>> Create([FromBody] CreateDebitNoteRequest req, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == req.InvoiceId, ct);
        if (invoice is null) return UnprocessableEntity(new { error = "unknown_invoice" });
        if (invoice.Status == InvoiceStatus.Draft || invoice.Status == InvoiceStatus.Cancelled)
            return UnprocessableEntity(new { error = "invoice_not_in_debitable_state" });

        var totalGst = Math.Round(req.SubtotalInr * req.GstRatePercent / 100m, 2);
        var (cgst, sgst, igst) = invoice.IsIntraState
            ? (Math.Round(totalGst - Math.Round(totalGst / 2m, 2), 2), Math.Round(totalGst / 2m, 2), 0m)
            : (0m, 0m, totalGst);

        var fy = InvoiceNumberGenerator.FinancialYearFor(req.IssueDate);
        var number = await _numberGen.NextForKindAsync("DBN", fy, ct);

        var dn = new DebitNote(number, fy, invoice.Id, invoice.ClientId,
            req.IssueDate, req.Reason, req.SubtotalInr, cgst, sgst, igst);
        _db.DebitNotes.Add(dn);
        await _db.SaveChangesAsync(ct);

        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == invoice.ClientId, ct);
        return CreatedAtAction(nameof(List), new { version = "1.0" },
            new DebitNoteDto(dn.Id, dn.Number, dn.FinancialYear, invoice.Id, invoice.Number,
                client.Id, client.LegalName, dn.IssueDate, dn.Reason,
                dn.SubtotalInr, dn.CgstInr, dn.SgstInr, dn.IgstInr, dn.TotalInr, dn.Status));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var dn = await _db.DebitNotes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dn is null) return NotFound();
        dn.Cancel();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
