using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Append-only timeline entry for a <see cref="ComplianceObligation"/>. Never updated.</summary>
public class ObligationEvent : Entity
{
    public Guid ObligationId { get; private set; }
    public ObligationEventKind Kind { get; private set; }

    public ComplianceStatus? FromStatus { get; private set; }
    public ComplianceStatus? ToStatus { get; private set; }

    public Guid? StaffId { get; private set; }
    public string? Note { get; private set; }

    public Guid? ActorUserId { get; private set; }
    public string? ActorUserName { get; private set; }

    public ComplianceObligation? Obligation { get; private set; }

    private ObligationEvent() { }

    public ObligationEvent(Guid obligationId, ObligationEventKind kind,
                            ComplianceStatus? fromStatus, ComplianceStatus? toStatus,
                            Guid? staffId, string? note,
                            Guid? actorUserId, string? actorUserName)
    {
        if (obligationId == Guid.Empty) throw new ArgumentException("ObligationId is required", nameof(obligationId));
        ObligationId = obligationId;
        Kind = kind;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        StaffId = staffId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ActorUserId = actorUserId;
        ActorUserName = string.IsNullOrWhiteSpace(actorUserName) ? null : actorUserName.Trim();
    }
}
