using System.Security.Cryptography;
using OtpNet;

namespace CMS.Infrastructure.Identity;

public interface ITotpService
{
    string GenerateBase32Secret();
    /// <summary>Build the <c>otpauth://totp</c> URI an authenticator app can scan.</summary>
    string BuildOtpAuthUri(string base32Secret, string issuer, string accountName);
    /// <summary>Return true when <paramref name="code"/> matches the current window ± 1 step.</summary>
    bool VerifyCode(string base32Secret, string code);
}

public class OtpNetTotpService : ITotpService
{
    public string GenerateBase32Secret()
    {
        // 20 random bytes → 32 base32 chars after padding is removed; matches RFC 6238 recommendation.
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encoding.ToString(bytes).TrimEnd('=');
    }

    public string BuildOtpAuthUri(string base32Secret, string issuer, string accountName)
    {
        var i = Uri.EscapeDataString(issuer);
        var a = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{i}:{a}?secret={base32Secret}&issuer={i}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code)) return false;
        code = new string(code.Where(char.IsDigit).ToArray());
        if (code.Length != 6) return false;
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }
}
