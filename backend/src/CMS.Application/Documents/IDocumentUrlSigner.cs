namespace CMS.Application.Documents;

/// <summary>Builds and verifies short-lived HMAC tokens for anonymous document downloads.</summary>
public interface IDocumentUrlSigner
{
    /// <summary>Returns the query-string segment (exp=…&v=…&sig=…) to append to the signed route.</summary>
    Task<SignedTokenParts> IssueAsync(Guid documentId, Guid? versionId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Returns true when the signature matches and has not expired.</summary>
    Task<bool> VerifyAsync(Guid documentId, Guid? versionId, long expUnix, string sigB64, CancellationToken ct = default);
}

public record SignedTokenParts(long ExpUnix, string SigBase64Url, DateTimeOffset ExpiresAtUtc);
