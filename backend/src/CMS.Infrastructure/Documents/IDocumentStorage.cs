namespace CMS.Infrastructure.Documents;

/// <summary>Abstraction over blob storage. v1 uses the local filesystem; S3/Azure Blob can slot in later.</summary>
public interface IDocumentStorage
{
    /// <summary>Save bytes under a stable key derived from the content hash. Idempotent: no-op if the key exists.</summary>
    Task SaveAsync(string storageKey, Stream content, CancellationToken ct);

    /// <summary>Open a read stream for the given storage key.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    bool Exists(string storageKey);

    /// <summary>Delete the stored blob. Only called when no document row references it anymore.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
