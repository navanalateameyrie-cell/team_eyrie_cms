using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Immutable record of an advance being consumed against an invoice.</summary>
public class AdvanceApplication : Entity
{
    public Guid AdvanceId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal AmountInr { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public bool IsReversed { get; private set; }

    public Advance? Advance { get; private set; }
    public Invoice? Invoice { get; private set; }

    private AdvanceApplication() { }

    public AdvanceApplication(Guid advanceId, Guid invoiceId, decimal amountInr)
    {
        if (advanceId == Guid.Empty) throw new ArgumentException("AdvanceId required", nameof(advanceId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId required", nameof(invoiceId));
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        AdvanceId = advanceId;
        InvoiceId = invoiceId;
        AmountInr = Math.Round(amountInr, 2, MidpointRounding.AwayFromZero);
    }

    public void Reverse()
    {
        if (IsReversed) return;
        IsReversed = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
