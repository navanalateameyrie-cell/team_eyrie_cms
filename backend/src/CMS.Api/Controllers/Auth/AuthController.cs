using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Asp.Versioning;
using CMS.Application.Abstractions;
using CMS.Application.Email;
using CMS.Application.Settings;
using CMS.Domain.Identity;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const int PasswordResetMinutes = 60;

    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IJwtTokenService _tokens;
    private readonly IRefreshTokenService _refresh;
    private readonly CmsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISettingsStore _settings;
    private readonly ICurrentUser _me;
    private readonly ITotpService _totp;
    private readonly IMfaPendingTokenService _mfaPending;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IJwtTokenService tokens,
        IRefreshTokenService refresh,
        CmsDbContext db,
        IEmailSender email,
        ISettingsStore settings,
        ICurrentUser me,
        ITotpService totp,
        IMfaPendingTokenService mfaPending,
        ILogger<AuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _refresh = refresh;
        _db = db;
        _email = email;
        _settings = settings;
        _me = me;
        _totp = totp;
        _mfaPending = mfaPending;
        _logger = logger;
    }

    public record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required] string Password);

    public record LoginResponse(
        string TokenType, string AccessToken, DateTimeOffset ExpiresAtUtc,
        string RefreshToken, DateTimeOffset RefreshExpiresAtUtc,
        string[] Roles, string[] Permissions);

    public record MfaChallengeRequiredResponse(bool MfaRequired, string MfaPendingToken);

    public record RefreshRequest([Required] string RefreshToken);
    public record LogoutRequest(string? RefreshToken);
    public record ForgotPasswordRequest([Required, EmailAddress] string Email);
    public record ResetPasswordRequest(
        [Required] string Token,
        [Required, MinLength(8)] string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !user.IsEnabled)
        {
            _logger.LogInformation("Login failed (unknown or disabled) for {Email}", req.Email);
            return Unauthorized(new { error = "invalid_credentials" });
        }

        var result = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogInformation("Login failed for {Email} (locked={Locked})", req.Email, result.IsLockedOut);
            return Unauthorized(new { error = result.IsLockedOut ? "locked_out" : "invalid_credentials" });
        }

        // If the user has an active TOTP enrolment, defer to the MFA challenge step.
        var mfa = await _db.UserMfaSecrets.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == user.Id, ct);
        if (mfa is { IsEnrolled: true })
        {
            var pending = _mfaPending.Issue(user.Id);
            return Ok(new MfaChallengeRequiredResponse(true, pending));
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);

        return Ok(await BuildLoginResponseAsync(user, ct));
    }

    // ---------------- MFA ----------------

    public record MfaChallengeRequest(
        [Required] string MfaPendingToken,
        [Required, StringLength(6, MinimumLength = 6)] string Code);

    public record MfaEnrolStartResponse(string Base32Secret, string OtpAuthUri);
    public record MfaEnrolVerifyRequest(
        [Required, StringLength(6, MinimumLength = 6)] string Code);
    public record MfaStatusResponse(bool IsEnrolled, DateTimeOffset? EnrolledAtUtc);

    [HttpPost("mfa/challenge")]
    [AllowAnonymous]
    public async Task<IActionResult> MfaChallenge([FromBody] MfaChallengeRequest req, CancellationToken ct)
    {
        var userId = _mfaPending.Validate(req.MfaPendingToken);
        if (userId is null) return Unauthorized(new { error = "invalid_pending_token" });

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsEnabled) return Unauthorized(new { error = "invalid_pending_token" });

        var mfa = await _db.UserMfaSecrets.FirstOrDefaultAsync(m => m.UserId == user.Id, ct);
        if (mfa is null || !mfa.IsEnrolled) return Unauthorized(new { error = "mfa_not_enrolled" });

        if (!_totp.VerifyCode(mfa.Base32Secret, req.Code))
            return Unauthorized(new { error = "invalid_code" });

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);

        return Ok(await BuildLoginResponseAsync(user, ct));
    }

    [HttpGet("mfa/status")]
    [Authorize]
    public async Task<ActionResult<MfaStatusResponse>> MfaStatus(CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var row = await _db.UserMfaSecrets.AsNoTracking().FirstOrDefaultAsync(m => m.UserId == _me.UserId.Value, ct);
        return new MfaStatusResponse(row?.IsEnrolled == true, row?.EnrolledAtUtc);
    }

    [HttpPost("mfa/enrol-start")]
    [Authorize]
    public async Task<ActionResult<MfaEnrolStartResponse>> MfaEnrolStart(CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var user = await _users.FindByIdAsync(_me.UserId.Value.ToString());
        if (user is null) return Unauthorized();

        var secret = _totp.GenerateBase32Secret();
        var row = await _db.UserMfaSecrets.FirstOrDefaultAsync(m => m.UserId == user.Id, ct);
        if (row is null)
        {
            row = new CMS.Domain.Identity.UserMfaSecret(user.Id, secret);
            _db.UserMfaSecrets.Add(row);
        }
        else
        {
            row.ReplaceSecret(secret);
        }
        await _db.SaveChangesAsync(ct);
        var uri = _totp.BuildOtpAuthUri(secret, issuer: "CMS", accountName: user.Email ?? user.UserName ?? user.Id.ToString());
        return new MfaEnrolStartResponse(secret, uri);
    }

    [HttpPost("mfa/enrol-verify")]
    [Authorize]
    public async Task<IActionResult> MfaEnrolVerify([FromBody] MfaEnrolVerifyRequest req, CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var row = await _db.UserMfaSecrets.FirstOrDefaultAsync(m => m.UserId == _me.UserId.Value, ct);
        if (row is null) return UnprocessableEntity(new { error = "enrolment_not_started" });
        if (!_totp.VerifyCode(row.Base32Secret, req.Code))
            return UnprocessableEntity(new { error = "invalid_code" });
        row.MarkEnrolled();
        await _db.SaveChangesAsync(ct);
        return Ok(new MfaStatusResponse(true, row.EnrolledAtUtc));
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> MfaDisable([FromBody] MfaEnrolVerifyRequest req, CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var row = await _db.UserMfaSecrets.FirstOrDefaultAsync(m => m.UserId == _me.UserId.Value, ct);
        if (row is null || !row.IsEnrolled)
            return UnprocessableEntity(new { error = "not_enrolled" });
        if (!_totp.VerifyCode(row.Base32Secret, req.Code))
            return UnprocessableEntity(new { error = "invalid_code" });
        row.Disable();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var existing = await _refresh.FindActiveAsync(req.RefreshToken, ct);
        if (existing is null) return Unauthorized(new { error = "invalid_refresh" });

        var user = await _users.FindByIdAsync(existing.UserId.ToString());
        if (user is null || !user.IsEnabled)
        {
            await _refresh.RevokeAsync(existing, "user_disabled_or_missing", ct);
            return Unauthorized(new { error = "invalid_refresh" });
        }

        var (raw, next) = await _refresh.RotateAsync(existing, ClientIp(), UserAgent(), ct);
        var access = await _tokens.IssueAsync(user, ct);
        var roles = await _users.GetRolesAsync(user);
        var perms = DecodePermissions(access.Token);

        return Ok(new LoginResponse(
            TokenType: access.TokenType,
            AccessToken: access.Token,
            ExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: raw,
            RefreshExpiresAtUtc: next.ExpiresAtUtc,
            Roles: roles.ToArray(),
            Permissions: perms));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req, CancellationToken ct)
    {
        if (req is not null && !string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            var existing = await _refresh.FindActiveAsync(req.RefreshToken, ct);
            if (existing is not null) await _refresh.RevokeAsync(existing, "logout", ct);
        }
        return NoContent();
    }

    [HttpPost("logout-everywhere")]
    [Authorize]
    public async Task<IActionResult> LogoutEverywhere(CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var revoked = await _refresh.RevokeAllForUserAsync(_me.UserId.Value, "logout_everywhere", ct);
        return Ok(new { revoked });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is not null && user.IsEnabled)
        {
            // Generate a token, store the hash, email the raw value via the configured sender.
            var raw = RandomUrlToken(48);
            var hash = Sha256Hex(raw);
            _db.PasswordResetTokens.Add(new PasswordResetToken(user.Id, hash,
                DateTimeOffset.UtcNow.AddMinutes(PasswordResetMinutes), ClientIp()));
            await _db.SaveChangesAsync(ct);

            var mail = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
            var origin = Request.Headers.Origin.ToString();
            if (string.IsNullOrWhiteSpace(origin)) origin = "https://cms.local";
            var link = $"{origin.TrimEnd('/')}/reset-password?token={raw}";
            try
            {
                await _email.SendAsync(new EmailMessage(
                    new[] { user.Email ?? req.Email }, Array.Empty<string>(),
                    Subject: "[CMS] Reset your password",
                    BodyPlainText:
                        $"A password reset was requested for your CMS account.\r\n\r\n" +
                        $"Open this link within {PasswordResetMinutes} minutes to set a new password:\r\n{link}\r\n\r\n" +
                        $"If you did not request this, ignore the message. The link only works once.",
                    Attachments: Array.Empty<EmailAttachment>()), ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Password reset email failed for {Email}", req.Email); }
        }
        // ALWAYS 200 — any other response leaks whether the email is registered.
        return Ok(new { status = "ok" });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var hash = Sha256Hex(req.Token);
        var record = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (record is null || !record.IsConsumable)
            return UnprocessableEntity(new { error = "invalid_or_expired_token" });

        var user = await _users.FindByIdAsync(record.UserId.ToString());
        if (user is null || !user.IsEnabled)
            return UnprocessableEntity(new { error = "invalid_or_expired_token" });

        // Validate policy by generating + consuming Identity's own reset token in-process. Avoids
        // RemovePassword/AddPassword's transactional edge cases on SQLite + concurrency stamps.
        var identityToken = await _users.GeneratePasswordResetTokenAsync(user);
        var setRes = await _users.ResetPasswordAsync(user, identityToken, req.NewPassword);
        if (!setRes.Succeeded)
            return UnprocessableEntity(new { error = "password_policy",
                detail = string.Join(";", setRes.Errors.Select(e => e.Description)) });

        record.Consume();
        await _refresh.RevokeAllForUserAsync(user.Id, "password_reset", ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "ok" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me([FromServices] ICurrentUser me) => Ok(new
    {
        userId = me.UserId,
        staffId = me.StaffId,
        userName = me.UserName,
        roles = me.Roles,
        permissions = me.Permissions
    });

    // ---- helpers ----

    private async Task<LoginResponse> BuildLoginResponseAsync(ApplicationUser user, CancellationToken ct)
    {
        var access = await _tokens.IssueAsync(user, ct);
        var (raw, record) = await _refresh.IssueAsync(user.Id, ClientIp(), UserAgent(), ct);
        var roles = await _users.GetRolesAsync(user);
        var perms = DecodePermissions(access.Token);
        return new LoginResponse(
            TokenType: access.TokenType,
            AccessToken: access.Token,
            ExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: raw,
            RefreshExpiresAtUtc: record.ExpiresAtUtc,
            Roles: roles.ToArray(),
            Permissions: perms);
    }

    private static string[] DecodePermissions(string jwt)
    {
        var decoded = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(jwt);
        return decoded.Claims
            .Where(c => c.Type == CMS.Application.Authorization.Permissions.ClaimType)
            .Select(c => c.Value).ToArray();
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent() =>
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private static string RandomUrlToken(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    private static string Sha256Hex(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
