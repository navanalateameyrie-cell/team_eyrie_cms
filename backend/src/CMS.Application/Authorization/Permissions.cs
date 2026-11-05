namespace CMS.Application.Authorization;

/// <summary>
/// Canonical permission catalog with granular Menu Access, Create, Update, Delete, and Special capabilities.
/// Format: <resource>.<action> — lowercase, dot-separated.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Clients
    {
        public const string Read = "clients.read";
        public const string Create = "clients.create";
        public const string Update = "clients.update";
        public const string Delete = "clients.delete";
        public const string Write = "clients.write"; // Legacy alias covering Create/Update
    }

    public static class Staff
    {
        public const string Read = "staff.read";
        public const string Create = "staff.create";
        public const string Update = "staff.update";
        public const string Delete = "staff.delete";
    }

    public static class Users
    {
        public const string Manage = "users.manage";
    }

    public static class Masters
    {
        public const string Read = "masters.read";
        public const string Create = "masters.create";
        public const string Update = "masters.update";
        public const string Delete = "masters.delete";
        public const string Write = "masters.write";
    }

    public static class Imports
    {
        public const string Read = "imports.read";
        public const string Create = "imports.create";
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
        public const string Create = "compliance.create";
        public const string Update = "compliance.update";
        public const string Delete = "compliance.delete";
        public const string Write = "compliance.write";
    }

    public static class Documents
    {
        public const string Read = "documents.read";
        public const string Create = "documents.create";
        public const string Update = "documents.update";
        public const string Delete = "documents.delete";
        public const string Approve = "documents.approve";
        public const string Write = "documents.write";
    }

    public static class Billing
    {
        public const string Read = "billing.read";
        public const string Create = "billing.create";
        public const string Update = "billing.update";
        public const string Delete = "billing.delete";
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
        public const string Read = "settings.read";
        public const string Manage = "settings.manage";
    }

    public static class Portal
    {
        public const string Access = "portal.access";
    }

    public static class Timesheet
    {
        public const string Read = "timesheet.read";
        public const string Create = "timesheet.create";
        public const string Update = "timesheet.update";
        public const string Approve = "timesheet.approve";
        public const string Write = "timesheet.write";
    }

    public static class Tds
    {
        public const string Read = "tds.read";
        public const string Create = "tds.create";
        public const string Update = "tds.update";
        public const string Write = "tds.write";
    }

    public static class Letters
    {
        public const string Read = "letters.read";
        public const string Create = "letters.create";
        public const string Update = "letters.update";
        public const string Delete = "letters.delete";
        public const string Write = "letters.write";
    }

    /// <summary>Grants everything. Master SuperAdmin only.</summary>
    public const string AdminFull = "admin.full";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        // Clients
        Clients.Read, Clients.Create, Clients.Update, Clients.Delete, Clients.Write,
        // Staff
        Staff.Read, Staff.Create, Staff.Update, Staff.Delete,
        // Users & Roles
        Users.Manage,
        // Masters
        Masters.Read, Masters.Create, Masters.Update, Masters.Delete, Masters.Write,
        // Imports & Exports
        Imports.Read, Imports.Create, Imports.Write, Exports.Perform,
        // Audit
        Audit.Read,
        // Compliance
        Compliance.Read, Compliance.Create, Compliance.Update, Compliance.Delete, Compliance.Write,
        // Documents
        Documents.Read, Documents.Create, Documents.Update, Documents.Delete, Documents.Approve, Documents.Write,
        // Billing
        Billing.Read, Billing.Create, Billing.Update, Billing.Delete, Billing.Write,
        // Reports
        Reports.Read,
        // Email
        Email.Send, Email.Read,
        // Settings
        SettingsPerms.Read, SettingsPerms.Manage,
        // Portal
        Portal.Access,
        // Timesheet
        Timesheet.Read, Timesheet.Create, Timesheet.Update, Timesheet.Approve, Timesheet.Write,
        // TDS
        Tds.Read, Tds.Create, Tds.Update, Tds.Write,
        // Letters
        Letters.Read, Letters.Create, Letters.Update, Letters.Delete, Letters.Write,
        // SuperAdmin
        AdminFull
    };
}
