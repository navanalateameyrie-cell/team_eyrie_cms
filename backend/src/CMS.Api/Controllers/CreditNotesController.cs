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
[Route("api/v{version:apiVersion}/billing/credit-notes")]
public class CreditNotesController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IInvoiceNumberGenerator _numberGen;

    public CreditNotesController(CmsDbContext db, IInvoiceNumberGenerator numberGen)
    { _db = db; _numberGen = numberGen; }

    public record CreditNoteDto(Guid Id, string Number, string FinancialYear,
        Guid InvoiceId, string InvoiceNumber, Guid ClientId, string ClientName,
        DateOnly IssueDate, string? Reason,
        decimal SubtotalInr, decimal CgstInr, decimal SgstInr, decimal IgstInr, decimal TotalInr,
        CreditNoteStatus Status);

    public record CreateCreditNoteRequest(
        [Required] Guid InvoiceId,
        [Required] DateOnly IssueDate,
        [StringLength(500)] string? Reason,
        [Range(0.01, 10_000_000)] decimal SubtotalInr,
        [Range(0, 100)] decimal GstRatePercent);

    [HttpGet]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<CreditNoteDto>>> List(
        [FromQuery] Guid? invoiceId, [FromQuery] Guid? clientId, CancellationToken ct)
    {
        var q = _db.CreditNotes.AsNoTracking().AsQueryable();
        if (invoiceId is not null) q = q.Where(x => x.InvoiceId == invoiceId);
        if (clientId is not null) q = q.Where(x => x.ClientId == clientId);
        return await (from n in q
                      join i in _db.Invoices.AsNoTracking() on n.InvoiceId equals i.Id
                      join c in _db.Clients.AsNoTracking() on n.ClientId equals c.Id
                      orderby n.CreatedAtUtc descending
                      select new CreditNoteDto(n.Id, n.Number, n.FinancialYear,
                          i.Id, i.Number, c.Id, c.LegalName,
                          n.IssueDate, n.Reason,
                          n.SubtotalInr, n.CgstInr, n.SgstInr, n.IgstInr, n.TotalInr,
                          n.Status)).ToListAsync(ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<CreditNoteDto>> Create([FromBody] CreateCreditNoteRequest req, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == req.InvoiceId, ct);
        if (invoice is null) return UnprocessableEntity(new { error = "unknown_invoice" });
        if (invoice.Status == InvoiceStatus.Draft || invoice.Status == InvoiceStatus.Cancelled)
            return UnprocessableEntity(new { error = "invoice_not_in_creditable_state" });

        var totalGst = Math.Round(req.SubtotalInr * req.GstRatePercent / 100m, 2);
        var (cgst, sgst, igst) = invoice.IsIntraState
            ? (Math.Round(totalGst - Math.Round(totalGst / 2m, 2), 2), Math.Round(totalGst / 2m, 2), 0m)
            : (0m, 0m, totalGst);
        var total = Math.Round(req.SubtotalInr + cgst + sgst + igst, 2);

        try { invoice.ApplyCredit(total); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "credit_exceeds_total", detail = ex.Message }); }

        var fy = InvoiceNumberGenerator.FinancialYearFor(req.IssueDate);
        var number = await _numberGen.NextForKindAsync("CRN", fy, ct);

        var cn = new CreditNote(number, fy, invoice.Id, invoice.ClientId,
            req.IssueDate, req.Reason, req.SubtotalInr, cgst, sgst, igst);
        _db.CreditNotes.Add(cn);
        await _db.SaveChangesAsync(ct);

        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == invoice.ClientId, ct);
        return CreatedAtAction(nameof(List), new { version = "1.0" },
            new CreditNoteDto(cn.Id, cn.Number, cn.FinancialYear, invoice.Id, invoice.Number,
                client.Id, client.LegalName, cn.IssueDate, cn.Reason,
                cn.SubtotalInr, cn.CgstInr, cn.SgstInr, cn.IgstInr, cn.TotalInr, cn.Status));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var cn = await _db.CreditNotes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cn is null) return NotFound();
        if (cn.Status == CreditNoteStatus.Cancelled) return NoContent();

        var invoice = await _db.Invoices.FirstAsync(i => i.Id == cn.InvoiceId, ct);
        invoice.ReverseCredit(cn.TotalInr);
        cn.Cancel();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
