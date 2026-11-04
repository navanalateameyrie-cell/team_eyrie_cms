using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Staff member master. A user account links to exactly one staff row (Phase 2).
/// Full staff lifecycle (departments, employment history) remains for a later phase.
/// </summary>
[Auditable]
public class Staff : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Email is PII — redacted from audit JSON by <see cref="SensitiveAuditAttribute"/>.</summary>
    [SensitiveAudit]
    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }
    public string? Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Staff() { }

    public Staff(string fullName, string email, string? code = null, string? phone = null, string? role = null, bool isActive = true)
    {
        SetCore(code, fullName, email, phone, role);
        IsActive = isActive;
    }

    public void UpdateProfile(string fullName, string email, string? code = null, string? phone = null, string? role = null)
    {
        SetCore(code, fullName, email, phone, role);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reactivate() { IsActive = true; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private void SetCore(string? code, string fullName, string email, string? phone, string? role)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name is required", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required", nameof(email));

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Code = string.IsNullOrWhiteSpace(code) ? GenerateCodeFromName(FullName) : code.Trim().ToUpperInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
    }

    private static string GenerateCodeFromName(string fullName)
    {
        // Deterministic fallback: first-letter initials + 4 chars of GUID. Admins can override via UI.
        var initials = string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(p => char.ToUpperInvariant(p[0])));
        if (initials.Length > 4) initials = initials[..4];
        return $"{initials}-{Guid.NewGuid():N}"[..Math.Min(12, initials.Length + 1 + 4)];
    }
}
