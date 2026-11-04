using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Archived prior state of a <see cref="Document"/>. Row inserted every time the document is
/// replaced; the parent row carries the current state. Oldest version is 1.
/// </summary>
[Auditable]
public class DocumentVersion : Entity
{
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;

    public Guid? UploaderUserId { get; private set; }
    public string? UploaderName { get; private set; }

    public Document? Document { get; private set; }

    private DocumentVersion() { }

    public DocumentVersion(Guid documentId, int versionNumber,
                            string originalFileName, string storageKey, string contentType,
                            long sizeBytes, string sha256,
                            Guid? uploaderUserId, string? uploaderName)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId required", nameof(documentId));
        if (versionNumber <= 0) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey required", nameof(storageKey));
        if (sha256 is null || sha256.Length != 64) throw new ArgumentException("Sha256 must be 64 hex chars", nameof(sha256));

        DocumentId = documentId;
        VersionNumber = versionNumber;
        OriginalFileName = originalFileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Sha256 = sha256.ToLowerInvariant();
        UploaderUserId = uploaderUserId;
        UploaderName = string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName!.Trim();
    }
}
