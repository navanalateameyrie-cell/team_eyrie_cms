using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A payment received from a client BEFORE an invoice is raised. Floating credit the practice
/// consumes against later invoices via <see cref="AdvanceApplication"/>.
/// </summary>
[Auditable]
public class Advance : Entity
{
    public Guid ClientId { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public decimal AmountInr { get; private set; }
    public decimal AmountAppliedInr { get; private set; }
    public PaymentMode Mode { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public bool IsReversed { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Client? Client { get; private set; }

    private Advance() { }

    public Advance(Guid clientId, DateOnly paidOn, decimal amountInr, PaymentMode mode,
                    string? reference, string? notes)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required", nameof(clientId));
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        ClientId = clientId;
        PaidOn = paidOn;
        AmountInr = Math.Round(amountInr, 2, MidpointRounding.AwayFromZero);
        Mode = mode;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Version = 1;
    }

    public decimal AmountRemainingInr => Math.Round(AmountInr - AmountAppliedInr, 2);

    public void Apply(decimal amount)
    {
        if (IsReversed) throw new InvalidOperationException("Reversed advance cannot be applied.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > AmountRemainingInr)
            throw new InvalidOperationException("Apply amount exceeds advance remainder.");
        AmountAppliedInr = Math.Round(AmountAppliedInr + amount, 2);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void ReverseApply(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AmountAppliedInr = Math.Round(Math.Max(0m, AmountAppliedInr - amount), 2);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void Reverse()
    {
        if (IsReversed) return;
        if (AmountAppliedInr > 0)
            throw new InvalidOperationException("Advance has been applied; reverse applications first.");
        IsReversed = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
