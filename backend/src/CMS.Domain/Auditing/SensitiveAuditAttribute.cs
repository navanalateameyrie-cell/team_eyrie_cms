namespace CMS.Domain.Auditing;

/// <summary>
/// Marks a property whose value must be redacted in audit old/new JSON (replaced with the
/// configured placeholder). The underlying column value is not modified.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SensitiveAuditAttribute : Attribute
{
    public string Placeholder { get; set; } = "***";
}
