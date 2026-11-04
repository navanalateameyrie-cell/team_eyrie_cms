using CMS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CMS.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var owned = context.User.FindAll(Permissions.ClaimType).Select(c => c.Value);
        foreach (var p in owned)
        {
            if (string.Equals(p, Permissions.AdminFull, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, requirement.Permission, StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }
        return Task.CompletedTask;
    }
}
