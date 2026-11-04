using System.Security.Cryptography;
using CMS.Domain.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Identity;

/// <summary>Issues and rotates opaque refresh tokens. The returned raw value is shown to the
/// client ONCE; only the SHA-256 hash lives in the DB.</summary>
public interface IRefreshTokenService
{
    /// <summary>Create a new refresh token for the user. Returns the raw value — persist nothing.</summary>
    Task<(string RawToken, RefreshToken Record)> IssueAsync(Guid userId, string? ip, string? userAgent, CancellationToken ct);

    /// <summary>Validate a raw token and, if valid, return the associated record (still active).</summary>
    Task<RefreshToken?> FindActiveAsync(string rawToken, CancellationToken ct);

    /// <summary>Atomically revoke the old token and issue a fresh one for the same user.</summary>
    Task<(string RawToken, RefreshToken Record)> RotateAsync(RefreshToken existing, string? ip, string? userAgent, CancellationToken ct);

    /// <summary>Revoke a specific token.</summary>
    Task RevokeAsync(RefreshToken existing, string reason, CancellationToken ct);

    /// <summary>Revoke every active refresh token for the user (logout-everywhere).</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct);
}

public class RefreshTokenService : IRefreshTokenService
{
    private const int RefreshTokenDays = 30;

    private readonly CmsDbContext _db;
    public RefreshTokenService(CmsDbContext db) => _db = db;

    public async Task<(string RawToken, RefreshToken Record)> IssueAsync(Guid userId, string? ip, string? userAgent, CancellationToken ct)
    {
        var raw = GenerateRaw();
        var hash = Hash(raw);
        var token = new RefreshToken(userId, hash,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenDays), ip, userAgent);
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return (raw, token);
    }

    public async Task<RefreshToken?> FindActiveAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = Hash(rawToken);
        var record = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        return record is not null && record.IsActive ? record : null;
    }

    public async Task<(string RawToken, RefreshToken Record)> RotateAsync(RefreshToken existing, string? ip, string? userAgent, CancellationToken ct)
    {
        var raw = GenerateRaw();
        var hash = Hash(raw);
        existing.Revoke("rotated", hash);

        var next = new RefreshToken(existing.UserId, hash,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenDays), ip, userAgent);
        _db.RefreshTokens.Add(next);
        await _db.SaveChangesAsync(ct);
        return (raw, next);
    }

    public async Task RevokeAsync(RefreshToken existing, string reason, CancellationToken ct)
    {
        existing.Revoke(reason);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        List<RefreshToken> active;
        if (_db.Database.IsPostgres())
        {
            active = await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
                .ToListAsync(ct);
        }
        else
        {
            // SQLite fallback — can't compare DateTimeOffset in SQL.
            var candidates = await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
                .ToListAsync(ct);
            active = candidates.Where(t => t.ExpiresAtUtc > now).ToList();
        }
        foreach (var t in active) t.Revoke(reason);
        await _db.SaveChangesAsync(ct);
        return active.Count;
    }

    private static string GenerateRaw()
    {
        // 48 bytes → 64 base64url chars, ample entropy.
        var buf = new byte[48];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string raw)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
