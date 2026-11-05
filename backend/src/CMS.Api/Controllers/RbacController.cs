using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Identity;
using CMS.Infrastructure.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class RbacController : ControllerBase
{
    private static readonly HashSet<string> SystemRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemAdmin", "Manager", "StaffUser", "ClientPortal"
    };

    private readonly CmsDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IRefreshTokenService _refreshTokenService;

    public RbacController(
        CmsDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IRefreshTokenService refreshTokenService)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _refreshTokenService = refreshTokenService;
    }

    public record PermissionMetaDto(string Id, string Name, string Description, string Group);

    public record RoleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystemRole,
        int UserCount,
        string[] Permissions);

    public record CreateRoleRequest(
        [Required, StringLength(100, MinimumLength = 2)] string Name,
        [StringLength(250)] string? Description,
        string[]? Permissions);

    public record UpdateRoleRequest(
        [StringLength(250)] string? Description,
        [Required] string[] Permissions);

    public record UserRbacDto(
        Guid Id,
        string Email,
        string UserName,
        Guid? StaffId,
        string? StaffName,
        string? StaffCode,
        Guid? ClientId,
        string? ClientName,
        string[] Roles,
        bool IsEnabled,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastLoginAtUtc);

    public record CreateUserRequest(
        [Required, EmailAddress, StringLength(256)] string Email,
        [StringLength(100)] string? Password,
        Guid? StaffId,
        string[]? Roles,
        bool IsEnabled = true);

    public record UpdateUserRolesRequest(
        [Required] string[] Roles);

    public record UpdateUserStatusRequest(
        bool IsEnabled);

    public record AdminResetPasswordRequest(
        [Required, MinLength(8)] string NewPassword);

    // ==========================================
    // 1. PERMISSION CATALOG
    // ==========================================

    [HttpGet("permissions")]
    [HasPermission(Permissions.Users.Manage)]
    public ActionResult<IReadOnlyList<PermissionMetaDto>> GetPermissions()
    {
        var catalog = new List<PermissionMetaDto>
        {
            // Practice Overview & Clients
            new(Permissions.Clients.Read, "View Clients", "Browse client directory, view entity profiles and contacts", "Client Management"),
            new(Permissions.Clients.Write, "Manage Clients", "Create, edit, and deactivate client entities and branches", "Client Management"),

            // Operations & Compliance
            new(Permissions.Compliance.Read, "View Compliance", "Access compliance obligations calendar and statutory deadlines", "Operations & Compliance"),
            new(Permissions.Compliance.Write, "Update Compliance", "File compliance returns, update status, and manage tasks", "Operations & Compliance"),
            new(Permissions.Timesheet.Read, "View Timesheets", "View time entries and tracked hours across engagements", "Operations & Compliance"),
            new(Permissions.Timesheet.Write, "Log Timesheets", "Record daily work hours and task descriptions", "Operations & Compliance"),
            new(Permissions.Timesheet.Approve, "Approve Timesheets", "Review and approve partner/manager timesheet submissions", "Operations & Compliance"),
            new(Permissions.Letters.Read, "View Letter Templates", "Access engagement letter templates and communication drafts", "Operations & Compliance"),
            new(Permissions.Letters.Write, "Manage Letter Templates", "Draft, update, and publish official letters and notices", "Operations & Compliance"),

            // Staff & Workload
            new(Permissions.Staff.Read, "View Staff & Workload", "View staff directory, workload capacity, and allocations", "Staff Management"),

            // Billing & Finance
            new(Permissions.Billing.Read, "View Invoices & Billing", "Browse invoices, payments, credit notes, and ledgers", "Billing & Finance"),
            new(Permissions.Billing.Write, "Manage Billing", "Create and issue invoices, credit notes, and record collections", "Billing & Finance"),
            new(Permissions.Tds.Read, "View TDS Ledgers", "View TDS receivables, 26AS matching, and certificates", "Billing & Finance"),
            new(Permissions.Tds.Write, "Manage TDS Ledgers", "Update TDS deductions, upload quarterly records and claims", "Billing & Finance"),

            // Documents
            new(Permissions.Documents.Read, "View Documents", "Access client documents, tax proofs, and workpapers", "Document Repository"),
            new(Permissions.Documents.Write, "Upload Documents", "Upload client files, KYC documents, and working sheets", "Document Repository"),
            new(Permissions.Documents.Delete, "Delete Documents", "Remove superseded or obsolete practice documents", "Document Repository"),
            new(Permissions.Documents.Approve, "Approve Documents", "Formally approve client tax audits and sign-off dossiers", "Document Repository"),

            // Masters & Administration
            new(Permissions.Masters.Read, "View Masters", "Read service catalogs, entity types, states, and client groups", "Reference Data"),
            new(Permissions.Masters.Write, "Manage Masters", "Create and edit reference masters and configuration tables", "Reference Data"),

            // Imports & Exports
            new(Permissions.Imports.Read, "View Data Imports", "Inspect past client staging batches and import logs", "Data Integration"),
            new(Permissions.Imports.Write, "Execute Data Imports", "Upload Excel/JSON files for bulk client data ingestion", "Data Integration"),
            new(Permissions.Exports.Perform, "Export Practice Data", "Download Excel, CSV, and summary reports", "Data Integration"),

            // Reporting & Audit
            new(Permissions.Reports.Read, "Access Reports", "Generate practice revenue, realization, and compliance reports", "Analytics & Reporting"),
            new(Permissions.Audit.Read, "Audit Explorer", "Search immutable audit logs and change histories", "Analytics & Reporting"),
            new(Permissions.Email.Read, "View Email Outbox", "Inspect system-generated emails and notification history", "Communication"),
            new(Permissions.Email.Send, "Send Practice Emails", "Trigger manual or automated client reminders and invoices", "Communication"),

            // Administration & Security
            new(Permissions.SettingsPerms.Manage, "Manage Practice Settings", "Update firm particulars, bank accounts, SMTP, and integrations", "Administration"),
            new(Permissions.Users.Manage, "Manage Users & RBAC", "Create users, configure roles, and assign security permissions", "Administration"),
            new(Permissions.Portal.Access, "Client Portal Access", "Granted to external portal users to view their account files", "External Access"),
            new(Permissions.AdminFull, "Full System SuperAdmin", "Unrestricted master administrative access over entire platform", "System Security"),
        };

        return Ok(catalog);
    }

    // ==========================================
    // 2. ROLES MANAGEMENT
    // ==========================================

    [HttpGet("roles")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken ct)
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(ct);
        var rolePermissions = await _db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var userRoles = await _db.UserRoles.AsNoTracking().ToListAsync(ct);

        var list = roles.Select(r =>
        {
            var isSystem = SystemRoleNames.Contains(r.Name ?? string.Empty);
            var perms = rolePermissions
                .Where(rp => rp.RoleId == r.Id)
                .Select(rp => rp.Permission)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            var userCount = userRoles.Count(ur => ur.RoleId == r.Id);

            return new RoleDto(
                r.Id,
                r.Name ?? string.Empty,
                r.Description,
                isSystem,
                userCount,
                perms);
        }).ToList();

        return Ok(list);
    }

    [HttpPost("roles")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest req, CancellationToken ct)
    {
        var trimmedName = req.Name.Trim();
        if (await _roleManager.RoleExistsAsync(trimmedName))
            return Conflict(new { error = "role_already_exists", message = $"Role '{trimmedName}' already exists." });

        var role = new ApplicationRole(trimmedName, req.Description?.Trim());
        var createResult = await _roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
            return UnprocessableEntity(new { error = "role_create_failed", detail = string.Join(";", createResult.Errors.Select(e => e.Description)) });

        var grantedPerms = new List<string>();
        if (req.Permissions is { Length: > 0 })
        {
            foreach (var p in req.Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Permissions.All.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    _db.RolePermissions.Add(new RolePermission(role.Id, p));
                    grantedPerms.Add(p);
                }
            }
            await _db.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, new RoleDto(
            role.Id,
            role.Name!,
            role.Description,
            false,
            0,
            grantedPerms.ToArray()));
    }

    [HttpPut("roles/{roleId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid roleId, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null) return NotFound(new { error = "role_not_found" });

        role.Description = req.Description?.Trim();
        await _roleManager.UpdateAsync(role);

        // Replace permissions
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);

        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in req.Permissions)
        {
            if (Permissions.All.Contains(p, StringComparer.OrdinalIgnoreCase))
            {
                granted.Add(p.ToLowerInvariant());
            }
        }

        // Ensure SystemAdmin never loses critical administrative root privileges
        if (string.Equals(role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
        {
            granted.Add(Permissions.AdminFull);
            granted.Add(Permissions.Users.Manage);
        }

        foreach (var p in granted)
        {
            _db.RolePermissions.Add(new RolePermission(roleId, p));
        }

        await _db.SaveChangesAsync(ct);

        var userCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);
        return Ok(new RoleDto(
            role.Id,
            role.Name ?? string.Empty,
            role.Description,
            SystemRoleNames.Contains(role.Name ?? string.Empty),
            userCount,
            granted.OrderBy(p => p).ToArray()));
    }

    [HttpDelete("roles/{roleId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null) return NotFound();

        if (SystemRoleNames.Contains(role.Name ?? string.Empty))
            return BadRequest(new { error = "system_role_immutable", message = "Built-in system roles cannot be deleted." });

        var inUse = await _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct);
        if (inUse)
            return BadRequest(new { error = "role_in_use", message = "Cannot delete a role that is assigned to active users." });

        var perms = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(perms);

        await _roleManager.DeleteAsync(role);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ==========================================
    // 3. USER ACCOUNTS & ASSIGNMENTS
    // ==========================================

    [HttpGet("users")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<IReadOnlyList<UserRbacDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(ct);
        var staffMap = await _db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var clientMap = await _db.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        var result = new List<UserRbacDto>();
        foreach (var u in users.OrderBy(u => u.Email))
        {
            var roles = await _userManager.GetRolesAsync(u);

            string? staffName = null;
            string? staffCode = null;
            if (u.StaffId.HasValue && staffMap.TryGetValue(u.StaffId.Value, out var s))
            {
                staffName = s.FullName;
                staffCode = s.Code;
            }

            string? clientName = null;
            if (u.ClientId.HasValue && clientMap.TryGetValue(u.ClientId.Value, out var c))
            {
                clientName = c.LegalName;
            }

            result.Add(new UserRbacDto(
                u.Id,
                u.Email ?? string.Empty,
                u.UserName ?? u.Email ?? string.Empty,
                u.StaffId,
                staffName,
                staffCode,
                u.ClientId,
                clientName,
                roles.ToArray(),
                u.IsEnabled,
                u.CreatedAtUtc,
                u.LastLoginAtUtc));
        }

        return Ok(result);
    }

    [HttpPost("users")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<UserRbacDto>> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
            return Conflict(new { error = "email_already_exists", message = $"User with email '{normalizedEmail}' already exists." });

        if (req.StaffId.HasValue && !await _db.Staff.AnyAsync(s => s.Id == req.StaffId.Value, ct))
            return UnprocessableEntity(new { error = "invalid_staff", message = "Referenced staff profile does not exist." });

        var password = string.IsNullOrWhiteSpace(req.Password) ? "Staff!23" : req.Password;

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            StaffId = req.StaffId,
            IsEnabled = req.IsEnabled,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return UnprocessableEntity(new { error = "user_create_failed", detail = string.Join(";", result.Errors.Select(e => e.Description)) });

        var assignedRoles = new List<string>();
        if (req.Roles is { Length: > 0 })
        {
            foreach (var roleName in req.Roles)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                    assignedRoles.Add(roleName);
                }
            }
        }
        else
        {
            // Default to StaffUser if no role provided
            if (await _roleManager.RoleExistsAsync("StaffUser"))
            {
                await _userManager.AddToRoleAsync(user, "StaffUser");
                assignedRoles.Add("StaffUser");
            }
        }

        string? staffName = null;
        string? staffCode = null;
        if (user.StaffId.HasValue)
        {
            var s = await _db.Staff.FindAsync(new object?[] { user.StaffId.Value }, ct);
            if (s is not null)
            {
                staffName = s.FullName;
                staffCode = s.Code;
            }
        }

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new UserRbacDto(
            user.Id,
            user.Email!,
            user.UserName!,
            user.StaffId,
            staffName,
            staffCode,
            null,
            null,
            assignedRoles.ToArray(),
            user.IsEnabled,
            user.CreatedAtUtc,
            null));
    }

    [HttpPut("users/{userId:guid}/roles")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<UserRbacDto>> UpdateUserRoles(Guid userId, [FromBody] UpdateUserRolesRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { error = "user_not_found" });

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Safeguard: Ensure at least one active SystemAdmin remains in the system
        if (currentRoles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) &&
            !req.Roles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase))
        {
            var adminRole = await _roleManager.FindByNameAsync("SystemAdmin");
            if (adminRole is not null)
            {
                var otherAdmins = await (from ur in _db.UserRoles
                                         join u in _db.Users on ur.UserId equals u.Id
                                         where ur.RoleId == adminRole.Id && u.Id != userId && u.IsEnabled
                                         select u).CountAsync(ct);

                if (otherAdmins == 0)
                {
                    return BadRequest(new
                    {
                        error = "last_system_admin",
                        message = "Cannot remove SystemAdmin role from the last active system administrator."
                    });
                }
            }
        }

        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        var validNewRoles = new List<string>();
        foreach (var r in req.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _roleManager.RoleExistsAsync(r))
            {
                validNewRoles.Add(r);
            }
        }

        if (validNewRoles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, validNewRoles);
        }

        // Revoke tokens so updated permissions take immediate effect upon subsequent token refresh
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "roles_updated", ct);

        string? staffName = null;
        string? staffCode = null;
        if (user.StaffId.HasValue)
        {
            var s = await _db.Staff.FindAsync(new object?[] { user.StaffId.Value }, ct);
            if (s is not null)
            {
                staffName = s.FullName;
                staffCode = s.Code;
            }
        }

        return Ok(new UserRbacDto(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.StaffId,
            staffName,
            staffCode,
            user.ClientId,
            null,
            validNewRoles.ToArray(),
            user.IsEnabled,
            user.CreatedAtUtc,
            user.LastLoginAtUtc));
    }

    [HttpPatch("users/{userId:guid}/status")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<UserRbacDto>> ToggleUserStatus(Guid userId, [FromBody] UpdateUserStatusRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { error = "user_not_found" });

        // Safeguard: Do not disable the last active SystemAdmin
        if (!req.IsEnabled)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase))
            {
                var adminRole = await _roleManager.FindByNameAsync("SystemAdmin");
                if (adminRole is not null)
                {
                    var otherAdmins = await (from ur in _db.UserRoles
                                             join u in _db.Users on ur.UserId equals u.Id
                                             where ur.RoleId == adminRole.Id && u.Id != userId && u.IsEnabled
                                             select u).CountAsync(ct);

                    if (otherAdmins == 0)
                    {
                        return BadRequest(new
                        {
                            error = "last_system_admin",
                            message = "Cannot disable the only remaining active System Administrator."
                        });
                    }
                }
            }
        }

        user.IsEnabled = req.IsEnabled;
        await _userManager.UpdateAsync(user);

        if (!user.IsEnabled)
        {
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, "account_disabled", ct);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        string? staffName = null;
        string? staffCode = null;
        if (user.StaffId.HasValue)
        {
            var s = await _db.Staff.FindAsync(new object?[] { user.StaffId.Value }, ct);
            if (s is not null)
            {
                staffName = s.FullName;
                staffCode = s.Code;
            }
        }

        return Ok(new UserRbacDto(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.StaffId,
            staffName,
            staffCode,
            user.ClientId,
            null,
            currentRoles.ToArray(),
            user.IsEnabled,
            user.CreatedAtUtc,
            user.LastLoginAtUtc));
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] AdminResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { error = "user_not_found" });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
        if (!result.Succeeded)
        {
            return UnprocessableEntity(new
            {
                error = "password_policy",
                detail = string.Join(";", result.Errors.Select(e => e.Description))
            });
        }

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "admin_password_reset", ct);
        return Ok(new { status = "ok", message = $"Password successfully reset for {user.Email}." });
    }
}
