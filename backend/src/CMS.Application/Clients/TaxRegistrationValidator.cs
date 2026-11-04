using System.Text.RegularExpressions;
using CMS.Domain.Entities;

namespace CMS.Application.Clients;

public static class TaxRegistrationValidator
{
    private static readonly Regex Gstin = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled);
    private static readonly Regex Tan   = new(@"^[A-Z]{4}[0-9]{5}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex Esic  = new(@"^[0-9]{17}$", RegexOptions.Compiled);
    private static readonly Regex Iec   = new(@"^[0-9A-Z]{10}$", RegexOptions.Compiled);

    public static string Normalize(string raw) => (raw ?? string.Empty).Trim().ToUpperInvariant();

    public static (bool ok, string? error) Validate(TaxRegistrationType type, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (false, "number_required");
        var n = Normalize(raw);
        return type switch
        {
            TaxRegistrationType.Gstin when !Gstin.IsMatch(n) => (false, "invalid_gstin_format"),
            TaxRegistrationType.Tan   when !Tan.IsMatch(n)   => (false, "invalid_tan_format"),
            TaxRegistrationType.Esic  when !Esic.IsMatch(n)  => (false, "invalid_esic_format"),
            TaxRegistrationType.Iec   when !Iec.IsMatch(n)   => (false, "invalid_iec_format"),
            // PT and PF formats vary widely by state / scheme — accept any non-blank value.
            _ => (true, null),
        };
    }
}
