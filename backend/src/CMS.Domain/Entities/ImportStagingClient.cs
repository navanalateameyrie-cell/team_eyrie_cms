using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A single raw client row, held in staging. Master rows (clients) are only ever created by
/// linking back to one of these rows through <see cref="CommittedClientId"/>. Nothing in the
/// production pipeline writes to <c>clients</c> without a corresponding staging row.
/// </summary>
public class ImportStagingClient : Entity
{
    public Guid BatchId { get; private set; }
    public int RowNumber { get; private set; }

    /// <summary>Raw JSON of the parsed cells, preserved for diagnostics.</summary>
    public string RawJson { get; private set; } = string.Empty;

    public string? NormalizedPan { get; private set; }
    public string? NormalizedLegalName { get; private set; }
    public string? NormalizedTradeName { get; private set; }
    public string? NormalizedLegacyClientId { get; private set; }
    public string? EntityTypeCode { get; private set; }
    public string? ClientGroupCode { get; private set; }

    public string? ValidationErrorsJson { get; private set; }   // JSON array of { code, detail }
    public string? CandidateClientIdsJson { get; private set; } // JSON array of guid

    public ImportDecision Decision { get; private set; } = ImportDecision.Pending;
    public Guid? DecidedClientId { get; private set; }
    public Guid? CommittedClientId { get; private set; }

    private ImportStagingClient() { }

    public ImportStagingClient(Guid batchId, int rowNumber, string rawJson)
    {
        BatchId = batchId;
        RowNumber = rowNumber;
        RawJson = rawJson ?? "{}";
    }

    public void SetNormalized(string? pan, string? legalName, string? tradeName,
                              string? legacyId, string? entityTypeCode, string? groupCode)
    {
        NormalizedPan = pan;
        NormalizedLegalName = legalName;
        NormalizedTradeName = tradeName;
        NormalizedLegacyClientId = legacyId;
        EntityTypeCode = entityTypeCode;
        ClientGroupCode = groupCode;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetValidation(string errorsJson, string candidatesJson)
    {
        ValidationErrorsJson = errorsJson;
        CandidateClientIdsJson = candidatesJson;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetDecision(ImportDecision decision, Guid? decidedClientId)
    {
        Decision = decision;
        DecidedClientId = decidedClientId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCommitted(Guid clientId)
    {
        CommittedClientId = clientId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
