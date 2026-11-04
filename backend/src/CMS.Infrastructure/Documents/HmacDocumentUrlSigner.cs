using System.Security.Cryptography;
using System.Text;
using CMS.Application.Documents;
using CMS.Application.Settings;

namespace CMS.Infrastructure.Documents;

/// <summary>HMAC-SHA256 signer. The shared secret lives under settings key `security.documentSigning`
/// (an auto-rotated per-install byte string). First use seeds it. Rotating the stored secret
/// invalidates every outstanding signed URL.</summary>
public sealed class HmacDocumentUrlSigner : IDocumentUrlSigner
{
    private const string SettingsKey = "security.documentSigning";
    private readonly ISettingsStore _settings;

    public HmacDocumentUrlSigner(ISettingsStore settings) => _settings = settings;

    public async Task<SignedTokenParts> IssueAsync(Guid documentId, Guid? versionId, TimeSpan ttl, CancellationToken ct = default)
    {
        var expUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var secret = await GetOrCreateSecretAsync(ct);
        var sig = Sign(secret, documentId, versionId, expUnix);
        return new SignedTokenParts(expUnix, sig, DateTimeOffset.FromUnixTimeSeconds(expUnix));
    }

    public async Task<bool> VerifyAsync(Guid documentId, Guid? versionId, long expUnix, string sigB64, CancellationToken ct = default)
    {
        if (expUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        var secret = await GetOrCreateSecretAsync(ct);
        var expected = Sign(secret, documentId, versionId, expUnix);
        try
        {
            var a = Base64UrlDecode(expected);
            var b = Base64UrlDecode(sigB64);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException) { return false; }
    }

    private static string Sign(byte[] secret, Guid docId, Guid? versionId, long expUnix)
    {
        var payload = Encoding.UTF8.GetBytes($"{docId:N}|{(versionId.HasValue ? versionId.Value.ToString("N") : "")}|{expUnix}");
        using var mac = new HMACSHA256(secret);
        return Base64UrlEncode(mac.ComputeHash(payload));
    }

    private async Task<byte[]> GetOrCreateSecretAsync(CancellationToken ct)
    {
        var raw = await _settings.GetRawAsync(SettingsKey, ct);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try { return Base64UrlDecode(raw.Trim('"')); } catch { /* re-seed */ }
        }
        var bytes = RandomNumberGenerator.GetBytes(32);
        var encoded = Base64UrlEncode(bytes);
        await _settings.SetAsync(SettingsKey, $"\"{encoded}\"", userId: null, userName: "system", ct);
        return bytes;
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var pad = s.Replace('-', '+').Replace('_', '/');
        switch (pad.Length % 4) { case 2: pad += "=="; break; case 3: pad += "="; break; }
        return Convert.FromBase64String(pad);
    }
}
