using CMS.Domain.Common;

namespace CMS.Domain.Identity;

/// <summary>
/// One row per user that has configured a TOTP authenticator. The base32 secret is persisted as-is
/// — the overall threat model for this record is the same as the password hash and the JWT signing
/// key, which also live in the DB. Treat DB access controls as the primary defence.
/// </summary>
public class UserMfaSecret : Entity
{
    public Guid UserId { get; private set; }
    public string Base32Secret { get; private set; } = string.Empty;
    public bool IsEnrolled { get; private set; }
    public DateTimeOffset? EnrolledAtUtc { get; private set; }
    public DateTimeOffset? DisabledAtUtc { get; private set; }

    private UserMfaSecret() { }

    public UserMfaSecret(Guid userId, string base32Secret)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(base32Secret)) throw new ArgumentException("Secret required", nameof(base32Secret));
        UserId = userId;
        Base32Secret = base32Secret.Trim();
    }

    public void MarkEnrolled()
    {
        IsEnrolled = true;
        EnrolledAtUtc = DateTimeOffset.UtcNow;
        DisabledAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        IsEnrolled = false;
        DisabledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ReplaceSecret(string base32Secret)
    {
        if (string.IsNullOrWhiteSpace(base32Secret)) throw new ArgumentException("Secret required", nameof(base32Secret));
        Base32Secret = base32Secret.Trim();
        IsEnrolled = false;
        EnrolledAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
