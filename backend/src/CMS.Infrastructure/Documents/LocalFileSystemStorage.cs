using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMS.Infrastructure.Documents;

public class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";
    /// <summary>Absolute filesystem path under which blobs are kept. Defaults to {ContentRoot}/App_Data/documents at startup.</summary>
    public string Root { get; set; } = string.Empty;
}

public class LocalFileSystemStorage : IDocumentStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileSystemStorage> _log;

    public LocalFileSystemStorage(IOptions<DocumentStorageOptions> opts, ILogger<LocalFileSystemStorage> log)
    {
        if (string.IsNullOrWhiteSpace(opts.Value.Root))
            throw new InvalidOperationException("DocumentStorage:Root is not configured.");
        _root = Path.GetFullPath(opts.Value.Root);
        Directory.CreateDirectory(_root);
        _log = log;
    }

    /// <summary>Resolve a storage key to an absolute file path and refuse any key that escapes the root.</summary>
    private string PathFor(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("storageKey is required", nameof(storageKey));
        if (storageKey.Contains("..") || Path.IsPathRooted(storageKey))
            throw new ArgumentException("storageKey must be a safe relative path", nameof(storageKey));
        var combined = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!combined.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved path escapes storage root.");
        return combined;
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct)
    {
        var full = PathFor(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (File.Exists(full)) return; // dedup: same hash → same bytes → nothing to write
        var temp = full + ".tmp";
        await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await content.CopyToAsync(fs, ct);
        File.Move(temp, full, overwrite: true);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var full = PathFor(storageKey);
        if (!File.Exists(full)) throw new FileNotFoundException("Document blob missing", full);
        Stream s = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(s);
    }

    public bool Exists(string storageKey)
    {
        try { return File.Exists(PathFor(storageKey)); }
        catch { return false; }
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var full = PathFor(storageKey);
        try { if (File.Exists(full)) File.Delete(full); }
        catch (Exception ex) { _log.LogWarning(ex, "Failed to delete blob {Key}", storageKey); }
        return Task.CompletedTask;
    }
}
