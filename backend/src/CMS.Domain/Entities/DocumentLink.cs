using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Polymorphic link that attaches a <see cref="Document"/> to one business entity. A single
/// document may link to several owners (e.g. a tax notice relevant to a client AND a specific
/// compliance obligation).
/// </summary>
public class DocumentLink : Entity
{
    public Guid DocumentId { get; private set; }
    public DocumentOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }

    public Document? Document { get; private set; }

    private DocumentLink() { }

    public DocumentLink(Guid documentId, DocumentOwnerType ownerType, Guid ownerId)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId is required", nameof(documentId));
        if (ownerId == Guid.Empty) throw new ArgumentException("OwnerId is required", nameof(ownerId));
        DocumentId = documentId;
        OwnerType = ownerType;
        OwnerId = ownerId;
    }
}
