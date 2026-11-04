namespace CMS.Infrastructure.Billing;

/// <summary>
/// Config for the e-invoicing (IRN/QR) provider. <c>Provider="Stub"</c> is the dev default and
/// what the test harness uses; <c>Provider="Nic"</c> hits the NIC sandbox or production IRP.
/// </summary>
public class EInvoicingOptions
{
    public const string SectionName = "EInvoicing";

    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Stub"; // "Stub" | "Nic"

    public string? BaseUrl { get; set; }       // NIC sandbox or production root
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Gstin { get; set; }         // the practice's GSTIN registered with IRP
    public string? Username { get; set; }
    public string? Password { get; set; }      // IRP user-password; store encrypted in a real deployment
}
