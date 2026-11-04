using CMS.Domain.Common;

namespace CMS.Domain.Identity;

/// <summary>
/// Opaque refresh token record. The token value itself is only ever shown to the client once at
/// issuance; we persist a hash so a DB leak doesn't surrender active sessions. Rotation on every
/// use is enforced by writing <see cref="RevokedAtUtc"/> and <see cref="ReplacedByTokenHash"/>
/// atomically with the issuance of the successor.
/// </summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the token string, hex-lowercase (64 chars).</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokeReason { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public string? CreatedByIp { get; private set; }
    public string? UserAgent { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc,
                         string? createdByIp, string? userAgent)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("TokenHash must be a 64-char hex SHA-256", nameof(tokenHash));

        UserId = userId;
        TokenHash = tokenHash.ToLowerInvariant();
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    public void Revoke(string reason, string? replacedByTokenHash = null)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = DateTimeOffset.UtcNow;
        RevokeReason = reason;
        ReplacedByTokenHash = string.IsNullOrWhiteSpace(replacedByTokenHash) ? null : replacedByTokenHash!.ToLowerInvariant();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
