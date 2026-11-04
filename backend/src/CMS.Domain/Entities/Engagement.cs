using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A single period's engagement under a Service File. Each year (and each period within a year)
/// is a NEW engagement — prior periods are never overwritten.
/// </summary>
[Auditable]
public class Engagement : Entity
{
    public Guid ServiceFileId { get; private set; }

    /// <summary>Indian financial year in "YYYY-YY" format, e.g. "2023-24".</summary>
    public string FinancialYear { get; private set; } = string.Empty;

    /// <summary>Assessment year "YYYY-YY" when relevant (direct-tax engagements), else null.</summary>
    public string? AssessmentYear { get; private set; }

    public EngagementPeriodType PeriodType { get; private set; } = EngagementPeriodType.Annual;

    /// <summary>Free-form sub-period label (e.g. "H1", "Q3", "Apr-2024"). Null when PeriodType is Annual.</summary>
    public string? PeriodLabel { get; private set; }

    public DateOnly PlannedStartDate { get; private set; }
    public DateOnly PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }

    public EngagementStatus Status { get; private set; } = EngagementStatus.Planned;
    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public ServiceFile? ServiceFile { get; private set; }

    private Engagement() { }

    public Engagement(Guid serviceFileId, string financialYear, string? assessmentYear,
                      EngagementPeriodType periodType, string? periodLabel,
                      DateOnly plannedStart, DateOnly plannedEnd, string? notes = null)
    {
        if (serviceFileId == Guid.Empty) throw new ArgumentException("ServiceFileId is required", nameof(serviceFileId));
        SetCore(financialYear, assessmentYear, periodType, periodLabel, plannedStart, plannedEnd, notes);
        ServiceFileId = serviceFileId;
        Status = EngagementStatus.Planned;
        Version = 1;
    }

    public void UpdateProfile(string financialYear, string? assessmentYear,
                              EngagementPeriodType periodType, string? periodLabel,
                              DateOnly plannedStart, DateOnly plannedEnd, string? notes)
    {
        if (Status is EngagementStatus.Completed or EngagementStatus.Cancelled)
            throw new InvalidOperationException($"{Status} engagements cannot be edited.");
        SetCore(financialYear, assessmentYear, periodType, periodLabel, plannedStart, plannedEnd, notes);
        Touch();
    }

    public void Start(DateOnly actualStart)
    {
        if (Status != EngagementStatus.Planned)
            throw new InvalidOperationException($"Cannot start a {Status} engagement.");
        Status = EngagementStatus.InProgress;
        ActualStartDate = actualStart;
        Touch();
    }

    public void Complete(DateOnly actualEnd)
    {
        if (Status != EngagementStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete a {Status} engagement.");
        if (ActualStartDate is not null && actualEnd < ActualStartDate)
            throw new ArgumentException("ActualEndDate cannot precede ActualStartDate.");
        Status = EngagementStatus.Completed;
        ActualEndDate = actualEnd;
        Touch();
    }

    public void Cancel()
    {
        if (Status is EngagementStatus.Completed or EngagementStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a {Status} engagement.");
        Status = EngagementStatus.Cancelled;
        Touch();
    }

    private void SetCore(string financialYear, string? assessmentYear,
                         EngagementPeriodType periodType, string? periodLabel,
                         DateOnly plannedStart, DateOnly plannedEnd, string? notes)
    {
        if (string.IsNullOrWhiteSpace(financialYear))
            throw new ArgumentException("FinancialYear is required", nameof(financialYear));
        if (plannedEnd < plannedStart)
            throw new ArgumentException("PlannedEndDate cannot precede PlannedStartDate.");

        FinancialYear = financialYear.Trim();
        AssessmentYear = string.IsNullOrWhiteSpace(assessmentYear) ? null : assessmentYear.Trim();
        PeriodType = periodType;
        PeriodLabel = periodType == EngagementPeriodType.Annual
            ? null
            : (string.IsNullOrWhiteSpace(periodLabel) ? null : periodLabel.Trim());
        PlannedStartDate = plannedStart;
        PlannedEndDate = plannedEnd;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
