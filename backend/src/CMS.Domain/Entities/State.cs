using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Jurisdictional state / province.</summary>
[Auditable]
public class State : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = "IN";
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private State() { }

    public State(string code, string name, string countryCode = "IN", int displayOrder = 0)
    {
        SetCore(code, name, countryCode, displayOrder);
    }

    public void Update(string code, string name, string countryCode, int displayOrder)
    {
        SetCore(code, name, countryCode, displayOrder);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reactivate() { IsActive = true; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private void SetCore(string code, string name, string countryCode, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(countryCode)) throw new ArgumentException("Country code is required", nameof(countryCode));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        DisplayOrder = displayOrder;
    }
}
