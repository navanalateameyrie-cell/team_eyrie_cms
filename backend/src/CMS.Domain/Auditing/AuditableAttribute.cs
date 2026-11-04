namespace CMS.Domain.Auditing;

/// <summary>
/// Marks an entity type as participating in the audit trail. Only entities with this attribute
/// are captured by <c>AuditingInterceptor</c> on insert/update/delete.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AuditableAttribute : Attribute
{
    /// <summary>Optional override for the <c>entity_type</c> column; defaults to the class name.</summary>
    public string? Name { get; set; }
}
