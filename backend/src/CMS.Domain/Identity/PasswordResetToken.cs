using CMS.Domain.Common;

namespace CMS.Domain.Identity;

/// <summary>Single-use, short-lived password-reset artefact; the raw token is never persisted.</summary>
public class PasswordResetToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }
    public string? CreatedByIp { get; private set; }

    private PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, string? createdByIp)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("TokenHash must be a 64-char hex SHA-256", nameof(tokenHash));
        UserId = userId;
        TokenHash = tokenHash.ToLowerInvariant();
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    public bool IsConsumable => UsedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    public void Consume()
    {
        if (UsedAtUtc is not null) return;
        UsedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
