using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A service the practice offers (GST filing, Tax audit, Advisory …).
/// Configurable master — clients opt in to services via a separate link entity (future phase);
/// services NEVER become columns on Client.
/// </summary>
[Auditable]
public class ServiceDefinition : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>JSON array of ServiceFieldDef describing fields the UI must collect on file create/edit.</summary>
    public string? RequiredFieldsJson { get; private set; }
    public string? OptionalFieldsJson { get; private set; }

    /// <summary>If set, the scheduler auto-rolls compliance obligations at this cadence for every active service file of this service.</summary>
    public ComplianceFrequency? Frequency { get; private set; }

    /// <summary>Days after a period's end when the obligation is due (e.g. 20 for GST monthly). Required when Frequency is set.</summary>
    public int? DueDayOffsetDays { get; private set; }

    private ServiceDefinition() { }

    public ServiceDefinition(string code, string name, string? category = null, string? description = null, int displayOrder = 0)
    {
        SetCore(code, name, category, description, displayOrder);
    }

    public void Update(string code, string name, string? category, string? description, int displayOrder)
    {
        SetCore(code, name, category, description, displayOrder);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetSchema(string? requiredFieldsJson, string? optionalFieldsJson)
    {
        RequiredFieldsJson = requiredFieldsJson;
        OptionalFieldsJson = optionalFieldsJson;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetCompliance(ComplianceFrequency? frequency, int? dueDayOffsetDays)
    {
        if (frequency is not null)
        {
            if (dueDayOffsetDays is null || dueDayOffsetDays < 0 || dueDayOffsetDays > 365)
                throw new ArgumentException("DueDayOffsetDays must be 0..365 when Frequency is set.", nameof(dueDayOffsetDays));
        }
        else
        {
            dueDayOffsetDays = null;
        }
        Frequency = frequency;
        DueDayOffsetDays = dueDayOffsetDays;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reactivate() { IsActive = true; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private void SetCore(string code, string name, string? category, string? description, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DisplayOrder = displayOrder;
    }
}
