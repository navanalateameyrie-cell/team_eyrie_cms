using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Key-value overlay of appsettings.json. A row here, if present, wins over the equivalent config
/// section for the next read; absent rows fall back to the IOptions default. Values are stored as
/// JSON so new setting kinds don't need a schema change.
/// </summary>
[Auditable]
public class AppSetting : Entity
{
    public string Key { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = string.Empty;
    public Guid? UpdatedByUserId { get; private set; }
    public string? UpdatedByUserName { get; private set; }

    private AppSetting() { }

    public AppSetting(string key, string valueJson, Guid? updatedByUserId, string? updatedByUserName)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required", nameof(key));
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("ValueJson is required", nameof(valueJson));
        Key = key.Trim();
        ValueJson = valueJson;
        UpdatedByUserId = updatedByUserId;
        UpdatedByUserName = string.IsNullOrWhiteSpace(updatedByUserName) ? null : updatedByUserName!.Trim();
    }

    public void Update(string valueJson, Guid? byUserId, string? byUserName)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("ValueJson is required", nameof(valueJson));
        ValueJson = valueJson;
        UpdatedByUserId = byUserId;
        UpdatedByUserName = string.IsNullOrWhiteSpace(byUserName) ? UpdatedByUserName : byUserName!.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
