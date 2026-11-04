namespace CMS.Application.Authorization;

/// <summary>
/// Canonical permission catalog. Keep in sync with any UI permission picker.
/// Format: &lt;resource&gt;.&lt;action&gt; — lowercase, dot-separated.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Clients
    {
        public const string Read = "clients.read";
        public const string Write = "clients.write";
    }

    public static class Staff
    {
        public const string Read = "staff.read";
    }

    public static class Users
    {
        public const string Manage = "users.manage";
    }

    public static class Masters
    {
        public const string Read = "masters.read";
        public const string Write = "masters.write";
    }

    public static class Imports
    {
        public const string Read = "imports.read";
        public const string Write = "imports.write";
    }

    public static class Exports
    {
        public const string Perform = "exports.perform";
    }

    public static class Audit
    {
        public const string Read = "audit.read";
    }

    public static class Compliance
    {
        public const string Read = "compliance.read";
        public const string Write = "compliance.write";
    }

    public static class Documents
    {
        public const string Read = "documents.read";
        public const string Write = "documents.write";
        public const string Delete = "documents.delete";
        public const string Approve = "documents.approve";
    }

    public static class Billing
    {
        public const string Read = "billing.read";
        public const string Write = "billing.write";
    }

    public static class Reports
    {
        public const string Read = "reports.read";
    }

    public static class Email
    {
        public const string Send = "email.send";
        public const string Read = "email.read";
    }

    public static class SettingsPerms
    {
        public const string Manage = "settings.manage";
    }

    public static class Portal
    {
        public const string Access = "portal.access";
    }

    public static class Timesheet
    {
        public const string Read = "timesheet.read";
        public const string Write = "timesheet.write";
        public const string Approve = "timesheet.approve";
    }

    public static class Tds
    {
        public const string Read = "tds.read";
        public const string Write = "tds.write";
    }

    public static class Letters
    {
        public const string Read = "letters.read";
        public const string Write = "letters.write";
    }

    /// <summary>Grants everything. SystemAdmin-only in dev seed.</summary>
    public const string AdminFull = "admin.full";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        Clients.Read,
        Clients.Write,
        Staff.Read,
        Users.Manage,
        Masters.Read,
        Masters.Write,
        Imports.Read,
        Imports.Write,
        Exports.Perform,
        Audit.Read,
        Compliance.Read,
        Compliance.Write,
        Documents.Read,
        Documents.Write,
        Documents.Delete,
        Documents.Approve,
        Billing.Read,
        Billing.Write,
        Reports.Read,
        Email.Send,
        Email.Read,
        SettingsPerms.Manage,
        Portal.Access,
        Timesheet.Read, Timesheet.Write, Timesheet.Approve,
        Tds.Read, Tds.Write,
        Letters.Read, Letters.Write,
        AdminFull
    };
}
