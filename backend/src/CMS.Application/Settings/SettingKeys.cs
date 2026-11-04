namespace CMS.Application.Settings;

/// <summary>Canonical setting-key constants shared between controllers and the store.</summary>
public static class SettingKeys
{
    public const string FirmProfile = "firmProfile";
    public const string Mail = "mail";
    public const string Reminders = "reminders";
    public const string EInvoicing = "eInvoicing";
    public const string DigitalSignature = "digitalSignature";

    public static readonly IReadOnlyCollection<string> All = new[] { FirmProfile, Mail, Reminders, EInvoicing, DigitalSignature };
}
