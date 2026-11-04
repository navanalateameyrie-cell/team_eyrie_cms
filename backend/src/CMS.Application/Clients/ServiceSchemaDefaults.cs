namespace CMS.Application.Clients;

/// <summary>
/// Default field schemas for the 13 standard service codes. Used by the dev seeder to
/// populate RequiredFieldsJson / OptionalFieldsJson on existing services where they're null.
/// Admins can override per-service via the Masters UI.
/// </summary>
public static class ServiceSchemaDefaults
{
    private const string FyPattern = @"^\d{4}-\d{2}$";

    public static IReadOnlyDictionary<string, (List<ServiceFieldDef> required, List<ServiceFieldDef> optional)>
        Catalog { get; } = new Dictionary<string, (List<ServiceFieldDef>, List<ServiceFieldDef>)>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["ACCOUNTS"] = (
                new() { Fy() },
                new() { Str("operatorInitials", "Operator initials", "e.g. B A S") }),
            ["IT"] = (
                new() { Ay() },
                new() { Str("incomeTaxId", "Income Tax portal ID") }),
            ["GST"] = (
                new() {
                    Str("branchId", "Branch", required: true, helpText: "Pick the branch this registration lives under"),
                    Select("periodicity", "Periodicity", new[] { "Monthly", "Quarterly" }, required: true),
                },
                new() { Str("gstnId", "GSTN portal ID") }),
            ["AGST"] = (
                new() { Str("branchId", "Branch", required: true), Fy() },
                new()),
            ["TDS"] = (
                new() {
                    Str("branchId", "Branch", required: true),
                    Select("quarter", "Quarter", new[] { "Q1", "Q2", "Q3", "Q4" }, required: true),
                    Fy(),
                },
                new() { Str("tracesId", "TRACES portal ID"), Str("sectionCode", "Section code") }),
            ["CERTIFICATION"] = (
                new() { Str("subject", "Certificate subject", required: true),
                        Date("issuedOn", "Issued on", required: true) },
                new()),
            ["ITOHEARINGS"] = (
                new() { Ay(), Date("noticeDate", "Notice date", required: true) },
                new() { Str("aoCode", "AO code") }),
            ["CITAPPEALS"] = (
                new() { Ay(), Str("appealNumber", "CIT(A) appeal no.", required: true) },
                new() { Str("aoCode", "AO code") }),
            ["ITATAPPEALS"] = (
                new() { Ay(), Str("appealNumber", "ITAT appeal no.", required: true) },
                new()),
            ["BANKAUDIT"] = (
                new() { Str("bankName", "Bank name", required: true),
                        Str("bankBranchCode", "Bank branch code", required: true),
                        Fy() },
                new() { Str("allotmentLetterRef", "Allotment letter ref."),
                        Str("auditeeBranchAllocation", "Allocation") }),
            ["ROC"] = (
                new() { Str("cin", "CIN", required: true, pattern: @"^[LUu][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$"),
                        Fy() },
                new()),
            ["ESIPF"] = (
                new() { Str("establishmentCode", "Establishment code", required: true) },
                new() { Str("pfRegNo", "PF reg no."), Str("esicRegNo", "ESIC reg no.") }),
            ["PT"] = (
                new() { Str("registrationNumber", "PT reg no.", required: true),
                        Str("stateCode", "State code", required: true, pattern: @"^[A-Z]{2,8}$") },
                new()),
        };

    private static ServiceFieldDef Fy() => new("financialYear", "Financial year", "string",
        Required: true, Pattern: FyPattern, Placeholder: "e.g. 2024-25");

    private static ServiceFieldDef Ay() => new("assessmentYear", "Assessment year", "string",
        Required: true, Pattern: FyPattern, Placeholder: "e.g. 2025-26");

    private static ServiceFieldDef Str(string name, string label, string? placeholder = null,
        string? pattern = null, bool required = false, string? helpText = null) =>
        new(name, label, "string", Required: required, Pattern: pattern,
            Placeholder: placeholder, HelpText: helpText);

    private static ServiceFieldDef Date(string name, string label, bool required = false) =>
        new(name, label, "date", Required: required);

    private static ServiceFieldDef Select(string name, string label, string[] options, bool required = false) =>
        new(name, label, "select", Required: required, Options: options);
}
