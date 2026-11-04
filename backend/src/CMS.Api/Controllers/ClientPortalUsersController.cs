using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Application.Email;
using CMS.Domain.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>Admin-side management of external client-portal user accounts.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/clients/{clientId:guid}/portal-users")]
public class ClientPortalUsersController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IEmailSender _email;

    public ClientPortalUsersController(CmsDbContext db,
                                        UserManager<ApplicationUser> users,
                                        RoleManager<ApplicationRole> roles,
                                        IEmailSender email)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _email = email;
    }

    public record InviteRequest(
        [Required, EmailAddress, StringLength(256)] string Email,
        [StringLength(100)] string? DisplayName);

    public record PortalUserDto(Guid UserId, string Email, string? DisplayName, bool IsEnabled, DateTimeOffset? LastLoginAtUtc);

    [HttpGet]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<IReadOnlyList<PortalUserDto>>> List(Guid clientId, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct)) return NotFound();
        return await _db.Users.AsNoTracking()
            .Where(u => u.ClientId == clientId)
            .Select(u => new PortalUserDto(u.Id, u.Email!, u.UserName, u.IsEnabled, u.LastLoginAtUtc))
            .ToListAsync(ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<PortalUserDto>> Invite(Guid clientId, [FromBody] InviteRequest req, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return UnprocessableEntity(new { error = "unknown_client" });

        if (await _users.FindByEmailAsync(req.Email) is not null)
            return Conflict(new { error = "email_already_exists" });

        if (await _roles.FindByNameAsync("ClientPortal") is null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "portal_role_missing" });

        // The invitee sets their own password via the password-reset flow — we never generate one here.
        var randomPlaceholder = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "A1!";

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = true,
            ClientId = clientId,
            IsEnabled = true,
        };
        var create = await _users.CreateAsync(user, randomPlaceholder);
        if (!create.Succeeded)
            return UnprocessableEntity(new { error = "user_create_failed",
                detail = string.Join(";", create.Errors.Select(e => e.Description)) });
        await _users.AddToRoleAsync(user, "ClientPortal");

        // Issue a password-reset token immediately so the invitee can set their own password.
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin)) origin = "https://cms.local";
        var link = $"{origin.TrimEnd('/')}/portal/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(req.Email)}";
        try
        {
            await _email.SendAsync(new EmailMessage(
                new[] { req.Email }, Array.Empty<string>(),
                Subject: "[CMS] Your client-portal invite",
                BodyPlainText:
                    "You have been invited to the CMS client portal.\r\n\r\n" +
                    "Set your password using this link (valid for 60 minutes):\r\n" + link + "\r\n\r\n" +
                    "After signing in, you'll see your invoices, obligations and documents.",
                Attachments: Array.Empty<EmailAttachment>()), ct);
        }
        catch { /* best-effort; admin can resend */ }

        return CreatedAtAction(nameof(List), new { clientId, version = "1.0" },
            new PortalUserDto(user.Id, user.Email!, user.UserName, user.IsEnabled, user.LastLoginAtUtc));
    }

    [HttpDelete("{userId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Deactivate(Guid clientId, Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.ClientId != clientId) return NotFound();
        user.IsEnabled = false;
        await _users.UpdateAsync(user);
        return NoContent();
    }
}
