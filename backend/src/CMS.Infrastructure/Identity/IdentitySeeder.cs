using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Domain.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Identity;

/// <summary>
/// Minimal dev seeding: three roles, their permissions, and one administrator account.
/// Idempotent — safe to call on every startup in Development; a no-op if roles already exist.
/// </summary>
public static class IdentitySeeder
{
    public record SeedResult(Guid AdminUserId, Guid AdminStaffId);

    public static readonly IReadOnlyDictionary<string, string[]> DevRoles =
        new Dictionary<string, string[]>
        {
            ["SystemAdmin"] = new[] { Permissions.AdminFull, Permissions.Users.Manage,
                                      Permissions.Clients.Read, Permissions.Clients.Write,
                                      Permissions.Staff.Read,
                                      Permissions.Masters.Read, Permissions.Masters.Write,
                                      Permissions.Imports.Read, Permissions.Imports.Write,
                                      Permissions.Exports.Perform,
                                      Permissions.Audit.Read,
                                      Permissions.Compliance.Read, Permissions.Compliance.Write,
                                      Permissions.Documents.Read, Permissions.Documents.Write, Permissions.Documents.Delete,
                                      Permissions.Documents.Approve,
                                      Permissions.Billing.Read, Permissions.Billing.Write,
                                      Permissions.Reports.Read,
                                      Permissions.Email.Send, Permissions.Email.Read,
                                      Permissions.SettingsPerms.Manage,
                                      Permissions.Timesheet.Read, Permissions.Timesheet.Write, Permissions.Timesheet.Approve,
                                      Permissions.Tds.Read, Permissions.Tds.Write,
                                      Permissions.Letters.Read, Permissions.Letters.Write },
            ["Manager"]     = new[] { Permissions.Clients.Read, Permissions.Clients.Write,
                                      Permissions.Staff.Read, Permissions.Masters.Read,
                                      Permissions.Imports.Read, Permissions.Exports.Perform,
                                      Permissions.Compliance.Read, Permissions.Compliance.Write,
                                      Permissions.Documents.Read, Permissions.Documents.Write, Permissions.Documents.Delete,
                                      Permissions.Documents.Approve,
                                      Permissions.Billing.Read, Permissions.Billing.Write,
                                      Permissions.Reports.Read,
                                      Permissions.Email.Send, Permissions.Email.Read,
                                      Permissions.Timesheet.Read, Permissions.Timesheet.Write, Permissions.Timesheet.Approve,
                                      Permissions.Tds.Read, Permissions.Tds.Write,
                                      Permissions.Letters.Read, Permissions.Letters.Write },
            ["StaffUser"]   = new[] { Permissions.Clients.Read, Permissions.Masters.Read,
                                      Permissions.Compliance.Read, Permissions.Compliance.Write,
                                      Permissions.Documents.Read, Permissions.Documents.Write,
                                      Permissions.Billing.Read, Permissions.Reports.Read,
                                      Permissions.Timesheet.Read, Permissions.Timesheet.Write,
                                      Permissions.Tds.Read, Permissions.Tds.Write,
                                      Permissions.Letters.Read, Permissions.Letters.Write },
            ["ClientPortal"] = new[] { Permissions.Portal.Access },
        };

    public const string DevAdminEmail = "admin@cms.local";
    public const string DevAdminPassword = "Admin!23";

    public static async Task<SeedResult> SeedAsync(
        CmsDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        foreach (var (roleName, perms) in DevRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName, $"Dev-seeded role: {roleName}");
                var create = await roleManager.CreateAsync(role);
                if (!create.Succeeded)
                    throw new InvalidOperationException($"Failed to create role {roleName}: {string.Join(";", create.Errors.Select(e => e.Description))}");
                logger?.LogInformation("Seeded role {Role}", roleName);
            }

            foreach (var perm in perms)
            {
                var exists = await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.Permission == perm, ct);
                if (!exists)
                {
                    db.RolePermissions.Add(new RolePermission(role.Id, perm));
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // Admin staff + user
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Email == DevAdminEmail, ct);
        if (staff is null)
        {
            staff = new Staff("System Administrator", DevAdminEmail);
            db.Staff.Add(staff);
            await db.SaveChangesAsync(ct);
        }

        var admin = await userManager.FindByEmailAsync(DevAdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = DevAdminEmail,
                Email = DevAdminEmail,
                EmailConfirmed = true,
                StaffId = staff.Id,
                IsEnabled = true
            };
            var created = await userManager.CreateAsync(admin, DevAdminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Failed to create admin user: {string.Join(";", created.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(admin, "SystemAdmin");
            logger?.LogInformation("Seeded dev admin user {Email}", DevAdminEmail);
        }

        return new SeedResult(admin.Id, staff.Id);
    }
}
