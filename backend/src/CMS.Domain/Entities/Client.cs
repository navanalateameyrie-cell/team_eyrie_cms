using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Client master. Code is a permanent 8-digit identifier assigned at creation and preserved for life
/// — not re-used on deactivation and not regenerated on reactivation.
/// </summary>
[Auditable]
public class Client : Entity
{
    public string Code { get; private set; } = string.Empty;

    /// <summary>Original identifier migrated from a legacy system; preserved separately from <see cref="Code"/>.</summary>
    public string? LegacyClientId { get; private set; }

    public string LegalName { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }

    /// <summary>Normalised PAN (upper-case). Duplicates are allowed; see duplicate-candidate handling in the API.</summary>
    public string? Pan { get; private set; }

    public Guid EntityTypeId { get; private set; }
    public Guid? ClientGroupId { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Optimistic concurrency token; incremented on every mutating method.</summary>
    [ConcurrencyCheck]
    public int Version { get; private set; }

    // Navigation (optional, for Includes)
    public EntityType? EntityType { get; private set; }
    public ClientGroup? ClientGroup { get; private set; }

    private Client() { }

    public Client(string code, string legalName, Guid entityTypeId,
                  string? tradeName = null, string? pan = null,
                  Guid? clientGroupId = null, string? legacyClientId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (code.Length != 8 || !code.All(char.IsDigit))
            throw new ArgumentException("Code must be exactly 8 digits", nameof(code));
        if (string.IsNullOrWhiteSpace(legalName)) throw new ArgumentException("Legal name is required", nameof(legalName));
        if (entityTypeId == Guid.Empty) throw new ArgumentException("Entity type is required", nameof(entityTypeId));

        Code = code;
        LegalName = legalName.Trim();
        EntityTypeId = entityTypeId;
        TradeName = Clean(tradeName);
        Pan = Clean(pan)?.ToUpperInvariant();
        ClientGroupId = clientGroupId;
        LegacyClientId = Clean(legacyClientId);
        Version = 1;
    }

    public void UpdateProfile(string legalName, string? tradeName, string? pan,
                              Guid entityTypeId, Guid? clientGroupId, string? legacyClientId)
    {
        if (string.IsNullOrWhiteSpace(legalName)) throw new ArgumentException("Legal name is required", nameof(legalName));
        if (entityTypeId == Guid.Empty) throw new ArgumentException("Entity type is required", nameof(entityTypeId));

        LegalName = legalName.Trim();
        TradeName = Clean(tradeName);
        Pan = Clean(pan)?.ToUpperInvariant();
        EntityTypeId = entityTypeId;
        ClientGroupId = clientGroupId;
        LegacyClientId = Clean(legacyClientId);
        Touch();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        Touch();
    }

    /// <summary>Reactivates the client WITHOUT regenerating the permanent code.</summary>
    public void Reactivate()
    {
        if (IsActive) return;
        IsActive = true;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
