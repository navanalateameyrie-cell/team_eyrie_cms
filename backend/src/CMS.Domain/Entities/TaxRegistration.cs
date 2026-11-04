using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A statutory registration attached to a branch (GSTIN, TAN, PT, PF, ESIC, IEC …).
/// Validation of the <see cref="Number"/> happens via <c>TaxRegistrationValidator</c> at the service layer.
/// </summary>
[Auditable]
public class TaxRegistration : Entity
{
    public Guid BranchId { get; private set; }
    public TaxRegistrationType Type { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; } = true;

    [ConcurrencyCheck]
    public int Version { get; private set; }

    private TaxRegistration() { }

    public TaxRegistration(Guid branchId, TaxRegistrationType type, string number,
                           DateOnly? effectiveFrom, DateOnly? effectiveTo)
    {
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required", nameof(branchId));
        SetCore(type, number, effectiveFrom, effectiveTo);
        BranchId = branchId;
        Version = 1;
    }

    public void Update(TaxRegistrationType type, string number,
                       DateOnly? effectiveFrom, DateOnly? effectiveTo)
    {
        SetCore(type, number, effectiveFrom, effectiveTo);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    private void SetCore(TaxRegistrationType type, string number, DateOnly? from, DateOnly? to)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Number is required", nameof(number));
        if (from is not null && to is not null && to < from)
            throw new ArgumentException("EffectiveTo cannot precede EffectiveFrom");

        Type = type;
        Number = number.Trim().ToUpperInvariant();
        EffectiveFrom = from;
        EffectiveTo = to;
    }
}
