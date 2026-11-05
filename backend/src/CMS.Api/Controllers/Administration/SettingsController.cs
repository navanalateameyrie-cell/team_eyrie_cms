using System.Text.Json;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Application.Settings;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Compliance;
using CMS.Infrastructure.Email;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Api.Controllers;

/// <summary>
/// Admin-only CRUD over the <c>app_settings</c> overlay. Writes validate the JSON body against
/// the known DTO shape so a typo doesn't break downstream consumers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settings")]
public class SettingsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ISettingsStore _store;
    private readonly ICurrentUser _user;

    public SettingsController(ISettingsStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public record SettingDto(string Key, object Value, DateTimeOffset? UpdatedAtUtc, string? UpdatedByUserName);
    public record SettingValueRequest(JsonElement Value);

    [HttpGet]
    [HasPermission(Permissions.SettingsPerms.Manage)]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> List(CancellationToken ct)
    {
        var raw = await _store.ListAsync(ct);
        var byKey = raw.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

        var items = new List<SettingDto>();
        foreach (var key in SettingKeys.All)
        {
            if (byKey.TryGetValue(key, out var row))
                items.Add(new SettingDto(key, JsonSerializer.Deserialize<JsonElement>(row.ValueJson, JsonOpts),
                                         row.UpdatedAtUtc, row.UpdatedByUserName));
            else
                items.Add(new SettingDto(key, await DefaultValueFor(key, ct), null, null));
        }
        return items;
    }

    [HttpGet("{key}")]
    [HasPermission(Permissions.SettingsPerms.Manage)]
    public async Task<ActionResult<SettingDto>> Get(string key, CancellationToken ct)
    {
        if (!SettingKeys.All.Contains(key, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = "unknown_setting_key", key });

        var raw = await _store.GetRawAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var all = await _store.ListAsync(ct);
            var row = all.First(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return new SettingDto(key, JsonSerializer.Deserialize<JsonElement>(raw!, JsonOpts),
                                   row.UpdatedAtUtc, row.UpdatedByUserName);
        }
        return new SettingDto(key, await DefaultValueFor(key, ct), null, null);
    }

    [HttpPut("{key}")]
    [HasPermission(Permissions.SettingsPerms.Manage)]
    public async Task<ActionResult<SettingDto>> Put(string key, [FromBody] SettingValueRequest req, CancellationToken ct)
    {
        if (!SettingKeys.All.Contains(key, StringComparer.OrdinalIgnoreCase))
            return NotFound(new { error = "unknown_setting_key", key });

        var canonical = SettingKeys.All.First(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        var json = req.Value.GetRawText();
        // Validate shape — the DTO deserialiser rejects malformed types; unknown fields are tolerated.
        var error = canonical switch
        {
            SettingKeys.FirmProfile => TryBind<FirmProfileOptions>(json),
            SettingKeys.Mail        => TryBind<MailOptions>(json),
            SettingKeys.Reminders   => TryBind<ReminderOptions>(json),
            SettingKeys.EInvoicing  => TryBind<EInvoicingOptions>(json),
            SettingKeys.DigitalSignature => TryBind<DigitalSignatureOptions>(json),
            _ => "unknown_setting_key",
        };
        if (error is not null) return UnprocessableEntity(new { error = "invalid_setting", detail = error });

        await _store.SetAsync(canonical, json, _user.UserId, _user.UserName, ct);
        return await Get(canonical, ct);
    }

    private static string? TryBind<T>(string json)
    {
        try { JsonSerializer.Deserialize<T>(json, JsonOpts); return null; }
        catch (JsonException ex) { return ex.Message; }
    }

    private async Task<object> DefaultValueFor(string key, CancellationToken ct)
    {
        return key switch
        {
            SettingKeys.FirmProfile => await _store.GetAsync<FirmProfileOptions>(key, ct),
            SettingKeys.Mail        => await _store.GetAsync<MailOptions>(key, ct),
            SettingKeys.Reminders   => await _store.GetAsync<ReminderOptions>(key, ct),
            SettingKeys.EInvoicing  => await _store.GetAsync<EInvoicingOptions>(key, ct),
            SettingKeys.DigitalSignature => await _store.GetAsync<DigitalSignatureOptions>(key, ct),
            _ => new { },
        };
    }
}
