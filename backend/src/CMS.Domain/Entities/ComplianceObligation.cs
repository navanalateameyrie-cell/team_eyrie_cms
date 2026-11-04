using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A single period's compliance due date for one service file (e.g. "GST return for Oct 2025, due 20-Nov").
/// Generated in bulk by the scheduler and transitioned through its lifecycle via explicit intents.
/// </summary>
[Auditable]
public class ComplianceObligation : Entity
{
    public Guid ServiceFileId { get; private set; }

    /// <summary>Stable identifier of the period within the file; e.g. "2025-10", "2025-Q2", "FY2024-25". Unique per (file, period).</summary>
    public string PeriodKey { get; private set; } = string.Empty;

    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public DateOnly DueDate { get; private set; }

    public ComplianceStatus Status { get; private set; } = ComplianceStatus.Pending;

    public Guid? AssignedStaffId { get; private set; }
    public DateOnly? FiledOn { get; private set; }

    /// <summary>Reference / acknowledgement number captured on filing (ARN, challan, receipt).</summary>
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public ServiceFile? ServiceFile { get; private set; }
    public Staff? AssignedStaff { get; private set; }

    private ComplianceObligation() { }

    public ComplianceObligation(Guid serviceFileId, string periodKey,
                                 DateOnly periodStart, DateOnly periodEnd, DateOnly dueDate)
    {
        if (serviceFileId == Guid.Empty) throw new ArgumentException("ServiceFileId is required", nameof(serviceFileId));
        if (string.IsNullOrWhiteSpace(periodKey)) throw new ArgumentException("PeriodKey is required", nameof(periodKey));
        if (periodEnd < periodStart) throw new ArgumentException("PeriodEnd cannot precede PeriodStart.");

        ServiceFileId = serviceFileId;
        PeriodKey = periodKey.Trim();
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
        Status = ComplianceStatus.Pending;
        Version = 1;
    }

    public void Assign(Guid? staffId)
    {
        AssignedStaffId = staffId;
        Touch();
    }

    public void SetDueDate(DateOnly dueDate)
    {
        DueDate = dueDate;
        Touch();
    }

    public void MarkInProgress()
    {
        if (Status is ComplianceStatus.Filed or ComplianceStatus.Waived)
            throw new InvalidOperationException($"Cannot move a {Status} obligation back to InProgress.");
        Status = ComplianceStatus.InProgress;
        Touch();
    }

    public void MarkFiled(DateOnly filedOn, string? reference)
    {
        if (Status == ComplianceStatus.Waived)
            throw new InvalidOperationException("Waived obligations cannot be filed.");
        Status = ComplianceStatus.Filed;
        FiledOn = filedOn;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Touch();
    }

    public void MarkDelayed()
    {
        if (Status is ComplianceStatus.Filed or ComplianceStatus.Waived)
            throw new InvalidOperationException($"Cannot mark a {Status} obligation as Delayed.");
        Status = ComplianceStatus.Delayed;
        Touch();
    }

    public void Waive(string? reason)
    {
        if (Status == ComplianceStatus.Filed)
            throw new InvalidOperationException("Filed obligations cannot be waived.");
        Status = ComplianceStatus.Waived;
        Notes = string.IsNullOrWhiteSpace(reason) ? Notes : reason.Trim();
        Touch();
    }

    public void SetNote(string? note)
    {
        Notes = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
