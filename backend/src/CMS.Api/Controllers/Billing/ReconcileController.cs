using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using CMS.Infrastructure.Reconciliation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/reconcile")]
public class ReconcileController : ControllerBase
{
    private const long MaxUploadBytes = 10L * 1024 * 1024;

    private readonly CmsDbContext _db;
    private readonly BankStatementParser _parser;
    private readonly IReconciliationService _recon;
    private readonly ICurrentUser _user;

    public ReconcileController(CmsDbContext db, BankStatementParser parser,
                                IReconciliationService recon, ICurrentUser user)
    { _db = db; _parser = parser; _recon = recon; _user = user; }

    public record ReconcileBatchDto(Guid Id, string FileName, long SizeBytes, DateTimeOffset UploadedAtUtc,
        string? UploadedByUserName, int LineCount, int MatchedInvoiceCount, int MatchedAdvanceCount,
        int IgnoredCount);

    public record ReconcileLineDto(Guid Id, Guid BatchId, DateOnly ValueDate, string Description,
        string? Reference, decimal AmountInr, BankStatementLineStatus Status,
        Guid? MatchedInvoiceId, string? MatchedInvoiceNumber, Guid? MatchedAdvanceId,
        string? MatchNote, int Version);

    public record ReconcileUploadResponse(Guid BatchId, int LineCount, int AutoMatched, int Ambiguous);

    public class UploadForm
    {
        public IFormFile? File { get; set; }
    }

    public record MatchInvoiceRequest(
        [Required] Guid InvoiceId,
        [Range(0.01, 10_000_000_000d)] decimal? AmountOverride,
        [StringLength(500)] string? Note);

    public record CreateAdvanceFromLineRequest(
        [Required] Guid ClientId,
        [StringLength(500)] string? Note);

    public record IgnoreLineRequest([StringLength(500)] string? Note);

    // ---------- Upload ----------

    [HttpPost("upload")]
    [HasPermission(Permissions.Billing.Write)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes + 4096)]
    public async Task<ActionResult<ReconcileUploadResponse>> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return UnprocessableEntity(new { error = "empty_file" });
        if (form.File.Length > MaxUploadBytes)
            return UnprocessableEntity(new { error = "file_too_large", maxBytes = MaxUploadBytes });

        string text;
        using (var sr = new StreamReader(form.File.OpenReadStream()))
            text = await sr.ReadToEndAsync(ct);

        List<ParsedBankLine> parsed;
        try { parsed = _parser.Parse(text); }
        catch (FormatException ex)
        {
            return UnprocessableEntity(new { error = "invalid_csv", detail = ex.Message });
        }

        var batch = new BankStatementBatch(form.File.FileName, form.File.Length, _user.UserId, _user.UserName);
        _db.BankStatementBatches.Add(batch);
        foreach (var p in parsed)
            _db.BankStatementLines.Add(new BankStatementLine(batch.Id, p.ValueDate, p.Description, p.Reference, p.AmountInr));
        await _db.SaveChangesAsync(ct);

        var auto = await _recon.AutoMatchBatchAsync(batch.Id, ct);
        await RollupBatchAsync(batch.Id, ct);
        return new ReconcileUploadResponse(batch.Id, parsed.Count, auto.Matched, auto.Ambiguous);
    }

    // ---------- Browse ----------

    [HttpGet("batches")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<ReconcileBatchDto>>> ListBatches(CancellationToken ct)
    {
        var rows = await _db.BankStatementBatches.AsNoTracking()
            .Select(b => new ReconcileBatchDto(b.Id, b.FileName, b.SizeBytes, b.CreatedAtUtc,
                b.UploadedByUserName, b.LineCount, b.MatchedInvoiceCount, b.MatchedAdvanceCount, b.IgnoredCount))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.UploadedAtUtc).ToList();
    }

    [HttpGet("batches/{id:guid}/lines")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<ReconcileLineDto>>> ListLines(Guid id,
        [FromQuery] BankStatementLineStatus? status, CancellationToken ct)
    {
        var q = _db.BankStatementLines.AsNoTracking().Where(l => l.BatchId == id);
        if (status is not null) q = q.Where(l => l.Status == status);

        var rows = await (from l in q
                          join i in _db.Invoices.AsNoTracking() on l.MatchedInvoiceId equals i.Id into ij
                          from i in ij.DefaultIfEmpty()
                          orderby l.ValueDate descending
                          select new ReconcileLineDto(l.Id, l.BatchId, l.ValueDate, l.Description, l.Reference,
                              l.AmountInr, l.Status,
                              l.MatchedInvoiceId, i == null ? null : i.Number,
                              l.MatchedAdvanceId, l.MatchNote, l.Version)).ToListAsync(ct);
        return rows;
    }

    // ---------- Manual actions ----------

    [HttpPost("lines/{id:guid}/match-invoice")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<ReconcileLineDto>> MatchInvoice(Guid id, [FromBody] MatchInvoiceRequest req, CancellationToken ct)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return NotFound();
        if (!line.IsCredit) return UnprocessableEntity(new { error = "line_is_not_a_credit" });

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == req.InvoiceId, ct);
        if (invoice is null) return UnprocessableEntity(new { error = "unknown_invoice" });

        var amount = req.AmountOverride ?? Math.Min(line.AmountInr,
            Math.Round(invoice.TotalInr - invoice.PaidInr - invoice.CreditedInr, 2));
        if (amount <= 0) return UnprocessableEntity(new { error = "nothing_to_apply" });

        try { invoice.RecordPayment(amount); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "cannot_pay", detail = ex.Message }); }
        var payment = new Payment(invoice.Id, line.ValueDate, amount, PaymentMode.Bank,
            line.Reference, req.Note ?? "manual-matched from bank statement");
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        line.MatchToInvoice(invoice.Id, payment.Id, req.Note);
        await _db.SaveChangesAsync(ct);
        await RollupBatchAsync(line.BatchId, ct);
        return await LoadLineAsync(id, ct);
    }

    [HttpPost("lines/{id:guid}/create-advance")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<ReconcileLineDto>> CreateAdvance(Guid id, [FromBody] CreateAdvanceFromLineRequest req, CancellationToken ct)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return NotFound();
        if (!line.IsCredit) return UnprocessableEntity(new { error = "line_is_not_a_credit" });
        if (!await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct))
            return UnprocessableEntity(new { error = "unknown_client" });

        var adv = new Advance(req.ClientId, line.ValueDate, line.AmountInr, PaymentMode.Bank,
            line.Reference, req.Note ?? "advance from bank statement");
        _db.Advances.Add(adv);
        await _db.SaveChangesAsync(ct);
        line.MatchToAdvance(adv.Id, req.Note);
        await _db.SaveChangesAsync(ct);
        await RollupBatchAsync(line.BatchId, ct);
        return await LoadLineAsync(id, ct);
    }

    [HttpPost("lines/{id:guid}/ignore")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<ReconcileLineDto>> Ignore(Guid id, [FromBody] IgnoreLineRequest? req, CancellationToken ct)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return NotFound();
        line.Ignore(req?.Note);
        await _db.SaveChangesAsync(ct);
        await RollupBatchAsync(line.BatchId, ct);
        return await LoadLineAsync(id, ct);
    }

    // ---------- helpers ----------

    private async Task<ReconcileLineDto> LoadLineAsync(Guid id, CancellationToken ct)
    {
        return await (from l in _db.BankStatementLines.AsNoTracking()
                      where l.Id == id
                      join i in _db.Invoices.AsNoTracking() on l.MatchedInvoiceId equals i.Id into ij
                      from i in ij.DefaultIfEmpty()
                      select new ReconcileLineDto(l.Id, l.BatchId, l.ValueDate, l.Description, l.Reference,
                          l.AmountInr, l.Status,
                          l.MatchedInvoiceId, i == null ? null : i.Number,
                          l.MatchedAdvanceId, l.MatchNote, l.Version)).FirstAsync(ct);
    }

    private async Task RollupBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await _db.BankStatementBatches.FirstAsync(b => b.Id == batchId, ct);
        var lines = await _db.BankStatementLines.AsNoTracking().Where(l => l.BatchId == batchId).ToListAsync(ct);
        batch.RollupCounts(
            lineCount: lines.Count,
            matchedInvoice: lines.Count(l => l.Status == BankStatementLineStatus.MatchedInvoice),
            matchedAdvance: lines.Count(l => l.Status == BankStatementLineStatus.MatchedAdvance),
            ignored: lines.Count(l => l.Status == BankStatementLineStatus.Ignored));
        await _db.SaveChangesAsync(ct);
    }
}
