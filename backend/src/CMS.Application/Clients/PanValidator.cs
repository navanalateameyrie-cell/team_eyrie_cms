using System.Text.RegularExpressions;

namespace CMS.Application.Clients;

/// <summary>Permanent Account Number (India) normalisation + format check.</summary>
public static class PanValidator
{
    private static readonly Regex PanRegex = new(@"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled);

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? normalized) =>
        !string.IsNullOrWhiteSpace(normalized) && PanRegex.IsMatch(normalized);

    /// <summary>Throws if the input is non-empty but doesn't match the PAN format.</summary>
    public static string? NormalizeOrThrow(string? raw)
    {
        var n = Normalize(raw);
        if (n is null) return null;
        if (!PanRegex.IsMatch(n))
            throw new ArgumentException($"PAN '{raw}' is not in the AAAAA9999A format.", nameof(raw));
        return n;
    }
}
