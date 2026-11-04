using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A single uploaded Excel/CSV file awaiting staged processing. The batch header carries the
/// SHA-256 of the uploaded bytes so re-uploads of the same file are rejected as duplicates
/// (unless the previous batch failed and never produced master rows).
/// </summary>
[Auditable]
public class ImportBatch : Entity
{
    public string FileName { get; private set; } = string.Empty;
    public string FileSha256 { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public ImportBatchStatus Status { get; private set; } = ImportBatchStatus.Received;
    public string? SourceSheet { get; private set; }
    public Guid? UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public int RowCount { get; private set; }
    public int CommittedClientCount { get; private set; }
    public int CommittedServiceFileCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; } = 1;

    private ImportBatch() { }

    public ImportBatch(string fileName, string sha256, long fileSize, Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("FileName is required", nameof(fileName));
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
            throw new ArgumentException("Sha256 must be 64 hex chars", nameof(sha256));
        FileName = fileName.Trim();
        FileSha256 = sha256.ToLowerInvariant();
        FileSizeBytes = fileSize;
        UploadedByUserId = userId;
    }

    public void TransitionTo(ImportBatchStatus next, string? sourceSheet = null)
    {
        // Only forward transitions through the normal pipeline; Failed / Cancelled are terminal.
        var valid =
            (Status == ImportBatchStatus.Received  && next == ImportBatchStatus.Parsed) ||
            (Status == ImportBatchStatus.Parsed    && next == ImportBatchStatus.Validated) ||
            (Status == ImportBatchStatus.Validated && next == ImportBatchStatus.Validated) ||
            (Status == ImportBatchStatus.Validated && next == ImportBatchStatus.Committed) ||
            (Status is ImportBatchStatus.Received or ImportBatchStatus.Parsed or ImportBatchStatus.Validated
                && next is ImportBatchStatus.Failed or ImportBatchStatus.Cancelled);
        if (!valid)
            throw new InvalidOperationException($"Cannot transition from {Status} to {next}.");

        Status = next;
        if (sourceSheet is not null) SourceSheet = sourceSheet;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void RecordCounts(int rowCount, int errorCount)
    {
        RowCount = rowCount;
        ErrorCount = errorCount;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void RecordCommit(int committedClients, int committedFiles)
    {
        CommittedClientCount = committedClients;
        CommittedServiceFileCount = committedFiles;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public void SetNotes(string notes)
    {
        Notes = notes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
