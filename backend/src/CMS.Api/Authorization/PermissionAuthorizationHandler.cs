using CMS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CMS.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var owned = context.User.FindAll(Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // SuperAdmin bypasses all permission checks
        if (owned.Contains(Permissions.AdminFull))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var req = requirement.Permission;

        // Exact match
        if (owned.Contains(req))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Backward compatibility: If an endpoint demands '<resource>.write',
        // having '<resource>.write', '<resource>.create', or '<resource>.update' satisfies it.
        if (req.EndsWith(".write", StringComparison.OrdinalIgnoreCase))
        {
            var resource = req[..^6];
            if (owned.Contains($"{resource}.create") ||
                owned.Contains($"{resource}.update") ||
                owned.Contains($"{resource}.delete"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        // Backward compatibility: If an endpoint demands '<resource>.read',
        // having '<resource>.read' or any write permission satisfies it.
        if (req.EndsWith(".read", StringComparison.OrdinalIgnoreCase))
        {
            var resource = req[..^5];
            if (owned.Contains($"{resource}.write") ||
                owned.Contains($"{resource}.create") ||
                owned.Contains($"{resource}.update"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
