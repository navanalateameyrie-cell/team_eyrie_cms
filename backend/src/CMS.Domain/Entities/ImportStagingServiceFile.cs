using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A single service-column cell lifted from a "Final" sheet, staged for turning into a ServiceFile
/// row at commit. One source row with N non-empty service columns produces N of these rows.
/// </summary>
public class ImportStagingServiceFile : Entity
{
    public Guid BatchId { get; private set; }
    public Guid SourceStagingClientId { get; private set; }

    public int RowNumber { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string? LegacyFileNumber { get; private set; }
    public string? Notes { get; private set; }

    public string? ValidationErrorsJson { get; private set; }

    public ImportDecision Decision { get; private set; } = ImportDecision.Pending;
    public Guid? CommittedServiceFileId { get; private set; }

    private ImportStagingServiceFile() { }

    public ImportStagingServiceFile(Guid batchId, Guid sourceStagingClientId,
                                    int rowNumber, string serviceCode,
                                    string? legacyFileNumber, string? notes)
    {
        BatchId = batchId;
        SourceStagingClientId = sourceStagingClientId;
        RowNumber = rowNumber;
        ServiceCode = (serviceCode ?? string.Empty).Trim().ToUpperInvariant();
        LegacyFileNumber = string.IsNullOrWhiteSpace(legacyFileNumber) ? null : legacyFileNumber.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void SetValidation(string errorsJson) { ValidationErrorsJson = errorsJson; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void SetDecision(ImportDecision d) { Decision = d; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkCommitted(Guid sfId) { CommittedServiceFileId = sfId; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}
