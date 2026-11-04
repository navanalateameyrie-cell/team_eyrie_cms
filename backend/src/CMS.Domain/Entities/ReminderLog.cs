using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Append-only trace of reminder emails dispatched for a given <see cref="ComplianceObligation"/>.
/// The de-dupe key is (ObligationId, Kind, SentOnDate) so the daily worker is safe to re-run on
/// the same calendar date without re-emailing anyone.
/// </summary>
public class ReminderLog : Entity
{
    public Guid ObligationId { get; private set; }
    public ReminderKind Kind { get; private set; }

    /// <summary>UTC calendar date on which the reminder was dispatched (not a timestamp).</summary>
    public DateOnly SentOnDate { get; private set; }

    public Guid? AssigneeStaffId { get; private set; }
    public string? AssigneeEmail { get; private set; }
    public Guid? OutboxEntryId { get; private set; }

    public ComplianceObligation? Obligation { get; private set; }

    private ReminderLog() { }

    public ReminderLog(Guid obligationId, ReminderKind kind, DateOnly sentOnDate,
                        Guid? assigneeStaffId, string? assigneeEmail, Guid? outboxEntryId)
    {
        if (obligationId == Guid.Empty) throw new ArgumentException("ObligationId is required", nameof(obligationId));
        ObligationId = obligationId;
        Kind = kind;
        SentOnDate = sentOnDate;
        AssigneeStaffId = assigneeStaffId;
        AssigneeEmail = string.IsNullOrWhiteSpace(assigneeEmail) ? null : assigneeEmail!.Trim();
        OutboxEntryId = outboxEntryId;
    }
}
