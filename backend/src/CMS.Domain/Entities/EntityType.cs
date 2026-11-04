using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Legal / organisational form a client may take (Individual, Private Limited, LLP, Partnership …).
/// A master row rather than an enum so admins can extend without a code change.
/// </summary>
[Auditable]
public class EntityType : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private EntityType() { }

    public EntityType(string code, string name, string? description = null, int displayOrder = 0)
    {
        SetCore(code, name, description, displayOrder);
    }

    public void Update(string code, string name, string? description, int displayOrder)
    {
        SetCore(code, name, description, displayOrder);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reactivate() { IsActive = true; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private void SetCore(string code, string name, string? description, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DisplayOrder = displayOrder;
    }
}
