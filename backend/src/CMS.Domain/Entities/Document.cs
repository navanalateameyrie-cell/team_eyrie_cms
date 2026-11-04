using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Uploaded binary (PDF, image, Office doc). Content lives in the configured storage root; this row
/// is the catalog entry. Deduped by <see cref="Sha256"/>: two uploads of the same bytes point at the
/// same <see cref="StorageKey"/> but keep separate document rows for independent lifecycle.
/// </summary>
[Auditable]
public class Document : Entity
{
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    /// <summary>Hex-encoded SHA-256 of the file bytes (lowercase, 64 chars).</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public string? Category { get; private set; }
    public string? Description { get; private set; }

    public Guid? UploaderUserId { get; private set; }
    public string? UploaderName { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    private Document() { }

    public Document(string originalFileName, string storageKey, string contentType,
                     long sizeBytes, string sha256,
                     string? category, string? description,
                     Guid? uploaderUserId, string? uploaderName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("OriginalFileName is required", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey is required", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("ContentType is required", nameof(contentType));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
            throw new ArgumentException("Sha256 must be a 64-char hex string", nameof(sha256));

        OriginalFileName = originalFileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Sha256 = sha256.Trim().ToLowerInvariant();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UploaderUserId = uploaderUserId;
        UploaderName = string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName.Trim();
        Version = 1;
    }

    public void SoftDelete(Guid? byUserId)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAtUtc = DateTimeOffset.UtcNow;
        DeletedByUserId = byUserId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void UpdateMetadata(string? category, string? description)
    {
        if (IsDeleted) throw new InvalidOperationException("Deleted documents cannot be edited.");
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    /// <summary>Replace the current blob + file metadata; the caller is responsible for archiving
    /// the previous state as a <see cref="DocumentVersion"/> row.</summary>
    public void ReplaceCurrent(string originalFileName, string storageKey, string contentType,
                                long sizeBytes, string sha256,
                                Guid? uploaderUserId, string? uploaderName)
    {
        if (IsDeleted) throw new InvalidOperationException("Deleted documents cannot be versioned.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey required", nameof(storageKey));
        if (sha256 is null || sha256.Length != 64) throw new ArgumentException("Sha256 must be 64 hex chars", nameof(sha256));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        OriginalFileName = originalFileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Sha256 = sha256.ToLowerInvariant();
        UploaderUserId = uploaderUserId;
        UploaderName = string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName!.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
