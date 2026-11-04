namespace CMS.Domain.Identity;

/// <summary>Join row granting a single permission string to a role. Composite PK (RoleId, Permission).</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public DateTimeOffset GrantedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    private RolePermission() { }

    public RolePermission(Guid roleId, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) throw new ArgumentException("Permission is required", nameof(permission));
        RoleId = roleId;
        Permission = permission.Trim().ToLowerInvariant();
    }
}
