using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Reconciliation;

public interface IReconciliationService
{
    Task<AutoMatchResult> AutoMatchBatchAsync(Guid batchId, CancellationToken ct = default);
}

public record AutoMatchResult(int Scanned, int Matched, int Ambiguous);

/// <summary>
/// Deterministic auto-matcher: for each unmatched credit line, finds open invoices whose
/// outstanding equals the line amount within ₹1. If reference contains the invoice number or the
/// invoice issue date is within 90 days of value date, that's a confirmed match. Exactly-one
/// confirmed candidate books a <see cref="Payment"/>; anything else leaves the line Unmatched.
/// </summary>
public class ReconciliationService : IReconciliationService
{
    private readonly CmsDbContext _db;
    private readonly ILogger<ReconciliationService> _log;

    public ReconciliationService(CmsDbContext db, ILogger<ReconciliationService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<AutoMatchResult> AutoMatchBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var lines = await _db.BankStatementLines
            .Where(l => l.BatchId == batchId
                        && l.Status == BankStatementLineStatus.Unmatched
                        && l.AmountInr > 0)
            .ToListAsync(ct);
        if (lines.Count == 0) return new AutoMatchResult(0, 0, 0);

        var open = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartPaid)
            .Select(i => new {
                i.Id, i.Number, i.IssueDate, i.ClientId,
                Outstanding = i.TotalInr - i.PaidInr - i.CreditedInr,
            })
            .ToListAsync(ct);

        int matched = 0, ambiguous = 0;

        foreach (var line in lines)
        {
            var candidates = open
                .Where(o => Math.Abs(o.Outstanding - line.AmountInr) <= 1m)
                .Select(o => new {
                    o.Id, o.Number, o.ClientId, o.Outstanding,
                    MatchesRef = !string.IsNullOrWhiteSpace(line.Reference)
                                 && line.Reference!.Contains(o.Number, StringComparison.OrdinalIgnoreCase),
                    MatchesDate = Math.Abs(line.ValueDate.DayNumber - o.IssueDate.DayNumber) <= 90,
                })
                .Where(c => c.MatchesRef || c.MatchesDate)
                .ToList();

            if (candidates.Count == 1)
            {
                var win = candidates[0];
                var invoice = await _db.Invoices.FirstAsync(i => i.Id == win.Id, ct);
                invoice.RecordPayment(win.Outstanding);
                var payment = new Payment(invoice.Id, line.ValueDate, win.Outstanding,
                    PaymentMode.Bank, line.Reference,
                    $"auto-matched from bank statement (line {line.Id})");
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct); // need payment.Id before linking the line

                line.MatchToInvoice(invoice.Id, payment.Id,
                    $"auto: {(win.MatchesRef ? "ref-matched" : "date-matched")} {invoice.Number}");
                // Update the in-memory open-list so a near-duplicate line doesn't double-match.
                open.RemoveAll(o => o.Id == win.Id);
                matched++;
            }
            else if (candidates.Count > 1)
            {
                ambiguous++;
                _log.LogDebug("Auto-match: line {LineId} has {N} candidates, leaving Unmatched",
                    line.Id, candidates.Count);
            }
        }

        await _db.SaveChangesAsync(ct);
        return new AutoMatchResult(lines.Count, matched, ambiguous);
    }
}
