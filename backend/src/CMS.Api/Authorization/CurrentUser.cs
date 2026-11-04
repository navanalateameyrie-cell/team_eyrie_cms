using System.Security.Claims;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;

namespace CMS.Api.Authorization;

public class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return;

        IsAuthenticated = true;
        UserName = principal.Identity.Name;

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var uid)) UserId = uid;

        var staff = principal.FindFirstValue("staff_id");
        if (Guid.TryParse(staff, out var sid)) StaffId = sid;

        var client = principal.FindFirstValue("portal_client_id");
        if (Guid.TryParse(client, out var cid)) PortalClientId = cid;

        Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        Permissions = principal.FindAll(CMS.Application.Authorization.Permissions.ClaimType)
                               .Select(c => c.Value).ToArray();
    }

    public bool IsAuthenticated { get; }
    public Guid? UserId { get; }
    public Guid? StaffId { get; }
    public Guid? PortalClientId { get; }
    public string? UserName { get; }
    public IReadOnlyCollection<string> Roles { get; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions { get; } = Array.Empty<string>();

    public bool HasPermission(string permission) =>
        Permissions.Contains(CMS.Application.Authorization.Permissions.AdminFull, StringComparer.OrdinalIgnoreCase) ||
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
