using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CMS.Infrastructure.Identity;

/// <summary>
/// Short-lived opaque token issued between a correct password check and the MFA challenge. We
/// don't persist these — the token is an HMAC over (userId, exp) signed with the JWT key, so
/// forgery requires knowing that key and expiry is encoded in the token itself.
/// </summary>
public interface IMfaPendingTokenService
{
    string Issue(Guid userId, TimeSpan? lifetime = null);
    Guid? Validate(string token);
}

public class MfaPendingTokenService : IMfaPendingTokenService
{
    private readonly byte[] _key;
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);

    public MfaPendingTokenService(IOptions<JwtOptions> jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.Value.SigningKey) || jwt.Value.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be configured.");
        _key = Encoding.UTF8.GetBytes(jwt.Value.SigningKey);
    }

    public string Issue(Guid userId, TimeSpan? lifetime = null)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime).ToUnixTimeSeconds();
        var payload = $"{userId:N}.{exp}";
        var sig = Convert.ToBase64String(SignUtf8(payload)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
        return $"{payload}.{sig}";
    }

    public Guid? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        var payload = $"{parts[0]}.{parts[1]}";
        var expected = Convert.ToBase64String(SignUtf8(payload)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
            return null;
        if (!long.TryParse(parts[1], out var exp)) return null;
        if (DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow) return null;
        if (!Guid.TryParseExact(parts[0], "N", out var userId)) return null;
        return userId;
    }

    private byte[] SignUtf8(string payload)
    {
        using var h = new HMACSHA256(_key);
        return h.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }
}
