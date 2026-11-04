using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A credit note issued against an existing <see cref="Invoice"/>. Reduces that invoice's net
/// receivable; it does NOT mutate the invoice line items. GST split mirrors the source invoice's
/// intra/inter-state determination at the moment of issue.
/// </summary>
[Auditable]
public class CreditNote : Entity
{
    public string Number { get; private set; } = string.Empty;
    public string FinancialYear { get; private set; } = string.Empty;

    public Guid InvoiceId { get; private set; }
    public Guid ClientId { get; private set; }

    public DateOnly IssueDate { get; private set; }
    public string? Reason { get; private set; }

    public decimal SubtotalInr { get; private set; }
    public decimal CgstInr { get; private set; }
    public decimal SgstInr { get; private set; }
    public decimal IgstInr { get; private set; }
    public decimal TotalInr { get; private set; }

    public CreditNoteStatus Status { get; private set; } = CreditNoteStatus.Issued;
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Invoice? Invoice { get; private set; }
    public Client? Client { get; private set; }

    private CreditNote() { }

    public CreditNote(string number, string financialYear, Guid invoiceId, Guid clientId,
                       DateOnly issueDate, string? reason,
                       decimal subtotalInr, decimal cgstInr, decimal sgstInr, decimal igstInr)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Number required", nameof(number));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId required", nameof(invoiceId));
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required", nameof(clientId));
        if (subtotalInr < 0 || cgstInr < 0 || sgstInr < 0 || igstInr < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotalInr), "Amounts must be non-negative.");

        Number = number.Trim();
        FinancialYear = financialYear.Trim();
        InvoiceId = invoiceId;
        ClientId = clientId;
        IssueDate = issueDate;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        SubtotalInr = Math.Round(subtotalInr, 2, MidpointRounding.AwayFromZero);
        CgstInr = Math.Round(cgstInr, 2, MidpointRounding.AwayFromZero);
        SgstInr = Math.Round(sgstInr, 2, MidpointRounding.AwayFromZero);
        IgstInr = Math.Round(igstInr, 2, MidpointRounding.AwayFromZero);
        TotalInr = Math.Round(SubtotalInr + CgstInr + SgstInr + IgstInr, 2, MidpointRounding.AwayFromZero);
        Version = 1;
    }

    public void Cancel()
    {
        if (Status == CreditNoteStatus.Cancelled) return;
        Status = CreditNoteStatus.Cancelled;
        CancelledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
