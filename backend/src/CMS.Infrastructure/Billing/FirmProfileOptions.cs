namespace CMS.Infrastructure.Billing;

/// <summary>
/// Practice-wide identity printed on tax invoices. Bound from the <c>FirmProfile</c> configuration
/// section; all fields are optional at bind time but the renderer falls back to sensible blanks so
/// an unconfigured dev environment still produces a legible PDF.
/// </summary>
public class FirmProfileOptions
{
    public const string SectionName = "FirmProfile";

    public string Name { get; set; } = "Chartered Accountants";
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Gstin { get; set; }
    public string? Pan { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public BankDetails Bank { get; set; } = new();
    public class BankDetails
    {
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }
        public string? Ifsc { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
    }
}
