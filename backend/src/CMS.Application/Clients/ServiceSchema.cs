using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CMS.Application.Clients;

/// <summary>Describes one field in a service's metadata schema.</summary>
public record ServiceFieldDef(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type,          // string | date | number | select
    [property: JsonPropertyName("required")] bool Required = false,
    [property: JsonPropertyName("pattern")] string? Pattern = null,
    [property: JsonPropertyName("options")] string[]? Options = null,
    [property: JsonPropertyName("placeholder")] string? Placeholder = null,
    [property: JsonPropertyName("helpText")] string? HelpText = null);

public record ServiceFieldValidationError(string FieldName, string Code, string? Detail = null);

public static class ServiceFieldSerializer
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<ServiceFieldDef> ReadFieldDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<ServiceFieldDef>>(json, JsonOpts) ?? new(); }
        catch { return new(); }
    }

    public static string WriteFieldDefs(IEnumerable<ServiceFieldDef> defs) =>
        JsonSerializer.Serialize(defs, JsonOpts);

    public static Dictionary<string, string> ReadValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Null => "",
                    JsonValueKind.String => p.Value.GetString() ?? "",
                    _ => p.Value.GetRawText(),
                };
            return dict;
        }
        catch { return new(); }
    }
}

/// <summary>
/// Validates a <c>MetadataJson</c> payload against a service's declared schema. Treats null/blank
/// values on required fields as errors; honours per-field pattern + select options.
/// </summary>
public static class ServiceFileMetadataValidator
{
    public static IReadOnlyList<ServiceFieldValidationError> Validate(
        string? requiredFieldsJson, string? optionalFieldsJson, string? metadataJson)
    {
        var required = ServiceFieldSerializer.ReadFieldDefs(requiredFieldsJson);
        var optional = ServiceFieldSerializer.ReadFieldDefs(optionalFieldsJson);
        var values = ServiceFieldSerializer.ReadValues(metadataJson);
        var errors = new List<ServiceFieldValidationError>();

        foreach (var def in required.Concat(optional))
        {
            values.TryGetValue(def.Name, out var raw);
            var empty = string.IsNullOrWhiteSpace(raw);
            if (def.Required && empty)
            {
                errors.Add(new(def.Name, "required"));
                continue;
            }
            if (empty) continue;

            if (!string.IsNullOrWhiteSpace(def.Pattern))
            {
                try
                {
                    if (!Regex.IsMatch(raw!, def.Pattern))
                        errors.Add(new(def.Name, "pattern_mismatch", def.Pattern));
                }
                catch { /* bad regex in schema — ignore */ }
            }
            if (def.Options is { Length: > 0 } opts
                && !opts.Contains(raw, StringComparer.OrdinalIgnoreCase))
                errors.Add(new(def.Name, "not_in_options", string.Join(",", opts)));
            if (def.Type == "date" && !DateOnly.TryParse(raw, out _))
                errors.Add(new(def.Name, "invalid_date"));
            if (def.Type == "number" && !decimal.TryParse(raw, out _))
                errors.Add(new(def.Name, "invalid_number"));
        }
        return errors;
    }
}
