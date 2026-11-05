using System.Text.Json.Serialization;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/imports")]
public class ImportsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IImportEngine _engine;
    private readonly ICurrentUser _user;

    public ImportsController(CmsDbContext db, IImportEngine engine, ICurrentUser user)
    {
        _db = db;
        _engine = engine;
        _user = user;
    }

    public record BatchDto(
        Guid Id, string FileName, string FileSha256, long FileSizeBytes,
        [property: JsonConverter(typeof(JsonStringEnumConverter))] ImportBatchStatus Status,
        string? SourceSheet, DateTimeOffset UploadedAtUtc,
        int RowCount, int CommittedClientCount, int CommittedServiceFileCount,
        int ErrorCount, string? Notes, int Version);

    public record StagingClientDto(
        Guid Id, Guid BatchId, int RowNumber,
        string? NormalizedPan, string? NormalizedLegalName, string? NormalizedTradeName,
        string? NormalizedLegacyClientId, string? EntityTypeCode, string? ClientGroupCode,
        string? ValidationErrorsJson, string? CandidateClientIdsJson,
        [property: JsonConverter(typeof(JsonStringEnumConverter))] ImportDecision Decision,
        Guid? DecidedClientId, Guid? CommittedClientId);

    public record StagingServiceFileDto(
        Guid Id, Guid BatchId, Guid SourceStagingClientId, int RowNumber,
        string ServiceCode, string? LegacyFileNumber, string? ValidationErrorsJson,
        [property: JsonConverter(typeof(JsonStringEnumConverter))] ImportDecision Decision,
        Guid? CommittedServiceFileId);

    public record UploadResponse(Guid BatchId, bool WasDuplicate, string Sha256);

    public record ReconciliationDto(
        BatchDto Batch,
        int Pending, int CreateNew, int UpdateExisting, int Skip,
        int ClientRowsWithErrors, int ServiceFileRowsWithErrors);

    public record DecisionRequest(
        [property: JsonConverter(typeof(JsonStringEnumConverter))] ImportDecision Decision,
        Guid? DecidedClientId);

    private static BatchDto Map(ImportBatch b) => new(
        b.Id, b.FileName, b.FileSha256, b.FileSizeBytes, b.Status, b.SourceSheet, b.UploadedAtUtc,
        b.RowCount, b.CommittedClientCount, b.CommittedServiceFileCount, b.ErrorCount, b.Notes, b.Version);

    [HttpGet]
    [HasPermission(Permissions.Imports.Read)]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> List(CancellationToken ct)
    {
        var rows = await _db.ImportBatches.AsNoTracking().ToListAsync(ct);
        return rows.OrderByDescending(b => b.UploadedAtUtc).Take(100).Select(Map).ToList();
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Imports.Read)]
    public async Task<ActionResult<BatchDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpGet("{id:guid}/rows")]
    [HasPermission(Permissions.Imports.Read)]
    public async Task<ActionResult<IReadOnlyList<StagingClientDto>>> ListRows(Guid id, CancellationToken ct)
    {
        var rows = await _db.ImportStagingClients.AsNoTracking()
            .Where(r => r.BatchId == id).OrderBy(r => r.RowNumber).ToListAsync(ct);
        return rows.Select(r => new StagingClientDto(
            r.Id, r.BatchId, r.RowNumber,
            r.NormalizedPan, r.NormalizedLegalName, r.NormalizedTradeName,
            r.NormalizedLegacyClientId, r.EntityTypeCode, r.ClientGroupCode,
            r.ValidationErrorsJson, r.CandidateClientIdsJson,
            r.Decision, r.DecidedClientId, r.CommittedClientId)).ToList();
    }

    [HttpGet("{id:guid}/service-file-rows")]
    [HasPermission(Permissions.Imports.Read)]
    public async Task<ActionResult<IReadOnlyList<StagingServiceFileDto>>> ListServiceFileRows(Guid id, CancellationToken ct)
    {
        var rows = await _db.ImportStagingServiceFiles.AsNoTracking()
            .Where(r => r.BatchId == id).OrderBy(r => r.RowNumber).ThenBy(r => r.ServiceCode).ToListAsync(ct);
        return rows.Select(r => new StagingServiceFileDto(
            r.Id, r.BatchId, r.SourceStagingClientId, r.RowNumber,
            r.ServiceCode, r.LegacyFileNumber, r.ValidationErrorsJson,
            r.Decision, r.CommittedServiceFileId)).ToList();
    }

    public class UploadForm
    {
        public IFormFile? File { get; set; }
    }

    [HttpPost("upload")]
    [HasPermission(Permissions.Imports.Write)]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResponse>> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        var file = form?.File;
        if (file is null || file.Length == 0) return BadRequest(new { error = "empty_file" });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var batchId = Guid.Empty;
        var result = await _engine.UploadAsync(file.FileName, bytes, _user.UserId, ct);
        batchId = result.BatchId;

        if (!result.WasDuplicate)
        {
            // Parse immediately so the client can see rows without a second round-trip.
            try { await _engine.ParseAsync(batchId, bytes, ct); }
            catch { /* engine already flipped batch to Failed */ }
        }

        return Ok(new UploadResponse(result.BatchId, result.WasDuplicate, result.Sha256));
    }

    [HttpPost("{id:guid}/validate")]
    [HasPermission(Permissions.Imports.Write)]
    public async Task<ActionResult<BatchDto>> Validate(Guid id, CancellationToken ct)
    {
        await _engine.ValidateAndMatchAsync(id, ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/auto-resolve")]
    [HasPermission(Permissions.Imports.Write)]
    public async Task<ActionResult<BatchDto>> AutoResolve(Guid id, CancellationToken ct)
    {
        await _engine.AutoResolveAsync(id, ct);
        return await Get(id, ct);
    }

    [HttpPatch("rows/{rowId:guid}")]
    [HasPermission(Permissions.Imports.Write)]
    public async Task<ActionResult<StagingClientDto>> SetDecision(Guid rowId, [FromBody] DecisionRequest req, CancellationToken ct)
    {
        var row = await _db.ImportStagingClients.FirstOrDefaultAsync(r => r.Id == rowId, ct);
        if (row is null) return NotFound();
        row.SetDecision(req.Decision, req.DecidedClientId);
        await _db.SaveChangesAsync(ct);
        return new StagingClientDto(
            row.Id, row.BatchId, row.RowNumber,
            row.NormalizedPan, row.NormalizedLegalName, row.NormalizedTradeName,
            row.NormalizedLegacyClientId, row.EntityTypeCode, row.ClientGroupCode,
            row.ValidationErrorsJson, row.CandidateClientIdsJson,
            row.Decision, row.DecidedClientId, row.CommittedClientId);
    }

    public record CommitDto(int ClientsCreated, int ClientsUpdated, int ServiceFilesCreated,
                            int RowsSkipped, int Errors);

    [HttpPost("{id:guid}/commit")]
    [HasPermission(Permissions.Imports.Write)]
    public async Task<ActionResult<CommitDto>> Commit(Guid id, CancellationToken ct)
    {
        var r = await _engine.CommitAsync(id, ct);
        return new CommitDto(r.ClientsCreated, r.ClientsUpdated, r.ServiceFilesCreated, r.RowsSkipped, r.Errors);
    }

    [HttpGet("{id:guid}/reconciliation")]
    [HasPermission(Permissions.Imports.Read)]
    public async Task<ActionResult<ReconciliationDto>> Reconciliation(Guid id, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return NotFound();
        var rows = await _db.ImportStagingClients.AsNoTracking()
            .Where(r => r.BatchId == id).ToListAsync(ct);
        var sfRows = await _db.ImportStagingServiceFiles.AsNoTracking()
            .Where(r => r.BatchId == id).ToListAsync(ct);

        int errClients = rows.Count(r => !string.IsNullOrWhiteSpace(r.ValidationErrorsJson) && r.ValidationErrorsJson != "[]");
        int errFiles = sfRows.Count(r => !string.IsNullOrWhiteSpace(r.ValidationErrorsJson) && r.ValidationErrorsJson != "[]");

        return new ReconciliationDto(
            Map(batch),
            rows.Count(r => r.Decision == ImportDecision.Pending),
            rows.Count(r => r.Decision == ImportDecision.CreateNew),
            rows.Count(r => r.Decision == ImportDecision.UpdateExisting),
            rows.Count(r => r.Decision == ImportDecision.Skip),
            errClients, errFiles);
    }
}
