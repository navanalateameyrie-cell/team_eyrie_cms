using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// One row from a bank statement upload. <see cref="AmountInr"/> is signed — credits are positive
/// (money in), debits negative. Only credit rows are candidates for invoice/advance matching;
/// debit rows are listed but typically marked Ignored.
/// </summary>
[Auditable]
public class BankStatementLine : Entity
{
    public Guid BatchId { get; private set; }

    public DateOnly ValueDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public decimal AmountInr { get; private set; }

    public BankStatementLineStatus Status { get; private set; } = BankStatementLineStatus.Unmatched;

    public Guid? MatchedInvoiceId { get; private set; }
    public Guid? MatchedPaymentId { get; private set; }
    public Guid? MatchedAdvanceId { get; private set; }
    public string? MatchNote { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public BankStatementBatch? Batch { get; private set; }

    private BankStatementLine() { }

    public BankStatementLine(Guid batchId, DateOnly valueDate, string description, string? reference, decimal amountInr)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("BatchId required", nameof(batchId));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description required", nameof(description));

        BatchId = batchId;
        ValueDate = valueDate;
        Description = description.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        AmountInr = Math.Round(amountInr, 2, MidpointRounding.AwayFromZero);
        Version = 1;
    }

    public bool IsCredit => AmountInr > 0;

    public void MatchToInvoice(Guid invoiceId, Guid paymentId, string? note)
    {
        if (Status == BankStatementLineStatus.MatchedInvoice) throw new InvalidOperationException("Already matched to an invoice.");
        MatchedInvoiceId = invoiceId;
        MatchedPaymentId = paymentId;
        MatchedAdvanceId = null;
        MatchNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
        Status = BankStatementLineStatus.MatchedInvoice;
        Touch();
    }

    public void MatchToAdvance(Guid advanceId, string? note)
    {
        MatchedAdvanceId = advanceId;
        MatchedInvoiceId = null;
        MatchedPaymentId = null;
        MatchNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
        Status = BankStatementLineStatus.MatchedAdvance;
        Touch();
    }

    public void Ignore(string? note)
    {
        MatchNote = string.IsNullOrWhiteSpace(note) ? MatchNote : note!.Trim();
        Status = BankStatementLineStatus.Ignored;
        Touch();
    }

    public void Reset()
    {
        MatchedInvoiceId = null; MatchedPaymentId = null; MatchedAdvanceId = null;
        Status = BankStatementLineStatus.Unmatched;
        Touch();
    }

    private void Touch() { UpdatedAtUtc = DateTimeOffset.UtcNow; Version++; }
}
