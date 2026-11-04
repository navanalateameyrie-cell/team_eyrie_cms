using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Staff time logged against a specific engagement for a specific day. Once approved, the row
/// becomes read-only — only an approver may unapprove it to allow further edits. Minutes are the
/// smallest unit of billable work; hours are a UI concern.
/// </summary>
[Auditable]
public class TimeEntry : Entity
{
    public Guid StaffId { get; private set; }
    public Guid EngagementId { get; private set; }

    public DateOnly WorkDate { get; private set; }
    public int Minutes { get; private set; }
    public bool Billable { get; private set; }

    public string? Notes { get; private set; }

    public bool IsApproved { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Staff? Staff { get; private set; }
    public Engagement? Engagement { get; private set; }

    private TimeEntry() { }

    public TimeEntry(Guid staffId, Guid engagementId, DateOnly workDate,
                      int minutes, bool billable, string? notes)
    {
        if (staffId == Guid.Empty) throw new ArgumentException("StaffId required", nameof(staffId));
        if (engagementId == Guid.Empty) throw new ArgumentException("EngagementId required", nameof(engagementId));
        if (minutes <= 0 || minutes > 24 * 60) throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be 1..1440");

        StaffId = staffId;
        EngagementId = engagementId;
        WorkDate = workDate;
        Minutes = minutes;
        Billable = billable;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Version = 1;
    }

    public void Edit(int minutes, bool billable, string? notes)
    {
        if (IsApproved) throw new InvalidOperationException("Approved entries cannot be edited; unapprove first.");
        if (minutes <= 0 || minutes > 24 * 60) throw new ArgumentOutOfRangeException(nameof(minutes));
        Minutes = minutes;
        Billable = billable;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }

    public void Approve(Guid approverUserId)
    {
        if (IsApproved) return;
        IsApproved = true;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        ApprovedByUserId = approverUserId == Guid.Empty ? null : approverUserId;
        Touch();
    }

    public void Unapprove()
    {
        if (!IsApproved) return;
        IsApproved = false;
        ApprovedAtUtc = null;
        ApprovedByUserId = null;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
