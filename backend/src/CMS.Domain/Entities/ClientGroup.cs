using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Logical grouping of clients — a corporate group, a family, a shared ownership cluster.
/// Clients attach to a group in a later phase via a nullable FK; the group itself is the master.
/// </summary>
[Auditable]
public class ClientGroup : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ClientGroup() { }

    public ClientGroup(string code, string name, string? description = null)
    {
        SetCore(code, name, description);
    }

    public void Update(string code, string name, string? description)
    {
        SetCore(code, name, description);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reactivate() { IsActive = true; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private void SetCore(string code, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
