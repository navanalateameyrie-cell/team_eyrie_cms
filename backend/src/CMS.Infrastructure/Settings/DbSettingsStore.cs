using System.Text.Json;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Compliance;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CMS.Infrastructure.Settings;

/// <summary>
/// Reads a DB-overlay for a given setting key and merges it on top of the IOptions default. The
/// store is scoped per request so a single request sees a consistent snapshot; subsequent requests
/// pick up any change automatically.
/// </summary>
public class DbSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly CmsDbContext _db;
    private readonly IOptions<FirmProfileOptions> _firmDefaults;
    private readonly IOptions<MailOptions> _mailDefaults;
    private readonly IOptions<ReminderOptions> _reminderDefaults;
    private readonly IOptions<EInvoicingOptions> _eInvoiceDefaults;
    private readonly IOptions<DigitalSignatureOptions> _dsDefaults;

    public DbSettingsStore(CmsDbContext db,
                            IOptions<FirmProfileOptions> firmDefaults,
                            IOptions<MailOptions> mailDefaults,
                            IOptions<ReminderOptions> reminderDefaults,
                            IOptions<EInvoicingOptions> eInvoiceDefaults,
                            IOptions<DigitalSignatureOptions> dsDefaults)
    {
        _db = db;
        _firmDefaults = firmDefaults;
        _mailDefaults = mailDefaults;
        _reminderDefaults = reminderDefaults;
        _eInvoiceDefaults = eInvoiceDefaults;
        _dsDefaults = dsDefaults;
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken ct = default) where T : class, new()
    {
        var raw = await GetRawAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parsed = JsonSerializer.Deserialize<T>(raw!, JsonOpts);
            if (parsed is not null) return parsed;
        }
        // Fall back to IOptions defaults for the three known types; return a fresh instance otherwise.
        return key switch
        {
            SettingKeys.FirmProfile when typeof(T) == typeof(FirmProfileOptions)
                => (T)(object)_firmDefaults.Value,
            SettingKeys.Mail when typeof(T) == typeof(MailOptions)
                => (T)(object)_mailDefaults.Value,
            SettingKeys.Reminders when typeof(T) == typeof(ReminderOptions)
                => (T)(object)_reminderDefaults.Value,
            SettingKeys.EInvoicing when typeof(T) == typeof(EInvoicingOptions)
                => (T)(object)_eInvoiceDefaults.Value,
            SettingKeys.DigitalSignature when typeof(T) == typeof(DigitalSignatureOptions)
                => (T)(object)_dsDefaults.Value,
            _ => new T(),
        };
    }

    public async Task<string?> GetRawAsync(string key, CancellationToken ct = default)
    {
        return await _db.AppSettings.AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.ValueJson).FirstOrDefaultAsync(ct);
    }

    public async Task SetAsync(string key, string valueJson, Guid? userId, string? userName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key is required", nameof(key));
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("valueJson is required", nameof(valueJson));

        // Validate JSON parses; the controller validates the shape against known DTOs separately.
        using (JsonDocument.Parse(valueJson)) { /* throws on invalid */ }

        var existing = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
            _db.AppSettings.Add(new AppSetting(key, valueJson, userId, userName));
        else
            existing.Update(valueJson, userId, userName);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<(string Key, string ValueJson, DateTimeOffset? UpdatedAtUtc, string? UpdatedByUserName)>> ListAsync(CancellationToken ct = default)
    {
        return (await _db.AppSettings.AsNoTracking()
            .Select(s => new { s.Key, s.ValueJson, s.UpdatedAtUtc, s.CreatedAtUtc, s.UpdatedByUserName })
            .ToListAsync(ct))
            .Select(s => (s.Key, s.ValueJson, (DateTimeOffset?)(s.UpdatedAtUtc ?? s.CreatedAtUtc), s.UpdatedByUserName))
            .ToList();
    }
}
