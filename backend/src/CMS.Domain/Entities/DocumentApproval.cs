using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Approval cycle on a <see cref="Document"/>. Append-first, then one terminal decision.</summary>
[Auditable]
public class DocumentApproval : Entity
{
    public Guid DocumentId { get; private set; }

    public Guid RequesterUserId { get; private set; }
    public string? RequesterName { get; private set; }
    public string? RequesterNote { get; private set; }

    public DocumentApprovalStatus Status { get; private set; } = DocumentApprovalStatus.Pending;

    public Guid? ApproverUserId { get; private set; }
    public string? ApproverName { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Document? Document { get; private set; }

    private DocumentApproval() { }

    public DocumentApproval(Guid documentId, Guid requesterUserId, string? requesterName, string? requesterNote)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId required", nameof(documentId));
        if (requesterUserId == Guid.Empty) throw new ArgumentException("RequesterUserId required", nameof(requesterUserId));
        DocumentId = documentId;
        RequesterUserId = requesterUserId;
        RequesterName = string.IsNullOrWhiteSpace(requesterName) ? null : requesterName!.Trim();
        RequesterNote = string.IsNullOrWhiteSpace(requesterNote) ? null : requesterNote!.Trim();
        Version = 1;
    }

    public void Decide(DocumentApprovalStatus decision, Guid approverUserId, string? approverName, string? note)
    {
        if (Status != DocumentApprovalStatus.Pending)
            throw new InvalidOperationException($"Approval already {Status}.");
        if (decision != DocumentApprovalStatus.Approved && decision != DocumentApprovalStatus.Rejected)
            throw new ArgumentException("decision must be Approved or Rejected", nameof(decision));
        if (approverUserId == Guid.Empty) throw new ArgumentException("ApproverUserId required", nameof(approverUserId));
        if (approverUserId == RequesterUserId)
            throw new InvalidOperationException("Requester cannot approve their own request.");

        Status = decision;
        ApproverUserId = approverUserId;
        ApproverName = string.IsNullOrWhiteSpace(approverName) ? null : approverName!.Trim();
        DecidedAtUtc = DateTimeOffset.UtcNow;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
