using CMS.Domain.Entities;

namespace CMS.Application.Abstractions;

public interface IImportEngine
{
    /// <summary>Hash the bytes, create (or re-attach to) an ImportBatch, persist the raw file reference.</summary>
    Task<UploadResult> UploadAsync(string fileName, byte[] content, Guid? userId, CancellationToken ct = default);

    Task ParseAsync(Guid batchId, byte[] content, CancellationToken ct = default);
    Task ValidateAndMatchAsync(Guid batchId, CancellationToken ct = default);
    Task AutoResolveAsync(Guid batchId, CancellationToken ct = default);
    Task<CommitResult> CommitAsync(Guid batchId, CancellationToken ct = default);
    Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken ct = default);
}

public record UploadResult(Guid BatchId, bool WasDuplicate, string Sha256);
public record CommitResult(int ClientsCreated, int ClientsUpdated, int ServiceFilesCreated,
                           int RowsSkipped, int Errors);
