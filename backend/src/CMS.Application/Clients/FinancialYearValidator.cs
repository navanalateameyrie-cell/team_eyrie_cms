using System.Text.RegularExpressions;

namespace CMS.Application.Clients;

/// <summary>Indian financial-year format: "YYYY-YY" where the two-digit suffix = (first-year + 1) mod 100.</summary>
public static class FinancialYearValidator
{
    private static readonly Regex Shape = new(@"^(\d{4})-(\d{2})$", RegexOptions.Compiled);

    public static bool IsValid(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var m = Shape.Match(raw.Trim());
        if (!m.Success) return false;
        var start = int.Parse(m.Groups[1].Value);
        var end = int.Parse(m.Groups[2].Value);
        return end == (start + 1) % 100;
    }

    public static string NormalizeOrThrow(string raw)
    {
        var n = (raw ?? string.Empty).Trim();
        if (!IsValid(n)) throw new ArgumentException($"Financial year '{raw}' is not in YYYY-YY form (e.g. '2023-24').");
        return n;
    }

    public static string? NormalizeAssessmentYear(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var n = raw.Trim();
        if (!IsValid(n)) throw new ArgumentException($"Assessment year '{raw}' is not in YYYY-YY form.");
        return n;
    }
}
