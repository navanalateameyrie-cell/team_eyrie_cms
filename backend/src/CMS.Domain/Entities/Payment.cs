using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>A single payment captured against an <see cref="Invoice"/>.</summary>
[Auditable]
public class Payment : Entity
{
    public Guid InvoiceId { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public decimal AmountInr { get; private set; }
    public PaymentMode Mode { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public bool IsReversed { get; private set; }

    public Invoice? Invoice { get; private set; }

    private Payment() { }

    public Payment(Guid invoiceId, DateOnly paidOn, decimal amountInr, PaymentMode mode,
                    string? reference, string? notes)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required", nameof(invoiceId));
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));

        InvoiceId = invoiceId;
        PaidOn = paidOn;
        AmountInr = Math.Round(amountInr, 2, MidpointRounding.AwayFromZero);
        Mode = mode;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Reverse()
    {
        if (IsReversed) return;
        IsReversed = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
