using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Standing fee for a service, optionally overridden per client. The invoice builder resolves
/// the most-specific effective rate (client-override → service default) at the time of invoice
/// creation; existing invoices are unaffected by later changes.
/// </summary>
[Auditable]
public class FeeSchedule : Entity
{
    public Guid ServiceDefinitionId { get; private set; }

    /// <summary>When set, this schedule is a client-specific override of the service default.</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>Base amount in INR, excluding GST.</summary>
    public decimal AmountInr { get; private set; }

    /// <summary>GST % applied by default (e.g. 18.00 for 18%).</summary>
    public decimal GstRatePercent { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public ServiceDefinition? ServiceDefinition { get; private set; }
    public Client? Client { get; private set; }

    private FeeSchedule() { }

    public FeeSchedule(Guid serviceDefinitionId, Guid? clientId,
                        decimal amountInr, decimal gstRatePercent,
                        DateOnly effectiveFrom, DateOnly? effectiveTo = null,
                        string? notes = null)
    {
        if (serviceDefinitionId == Guid.Empty) throw new ArgumentException("ServiceDefinitionId is required", nameof(serviceDefinitionId));
        SetCore(amountInr, gstRatePercent, effectiveFrom, effectiveTo, notes);
        ServiceDefinitionId = serviceDefinitionId;
        ClientId = clientId == Guid.Empty ? null : clientId;
        Version = 1;
    }

    public void UpdateProfile(decimal amountInr, decimal gstRatePercent,
                               DateOnly effectiveFrom, DateOnly? effectiveTo, string? notes)
    {
        SetCore(amountInr, gstRatePercent, effectiveFrom, effectiveTo, notes);
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Reactivate() { IsActive = true; Touch(); }

    private void SetCore(decimal amountInr, decimal gstRatePercent,
                          DateOnly effectiveFrom, DateOnly? effectiveTo, string? notes)
    {
        if (amountInr < 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        if (gstRatePercent < 0 || gstRatePercent > 100) throw new ArgumentOutOfRangeException(nameof(gstRatePercent));
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
            throw new ArgumentException("EffectiveTo cannot precede EffectiveFrom.");
        AmountInr = Math.Round(amountInr, 2, MidpointRounding.AwayFromZero);
        GstRatePercent = Math.Round(gstRatePercent, 2, MidpointRounding.AwayFromZero);
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
