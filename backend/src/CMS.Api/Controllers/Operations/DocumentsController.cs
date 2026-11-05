using System.Security.Cryptography;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Application.Documents;
using CMS.Domain.Entities;
using CMS.Infrastructure.Documents;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxUploadBytes = 50L * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",       // xlsx
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // docx
        "application/vnd.openxmlformats-officedocument.presentationml.presentation", // pptx
        "application/vnd.ms-excel", "application/msword", "application/vnd.ms-powerpoint",
        "text/csv", "text/plain",
        "application/zip",
    };

    private readonly CmsDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly ICurrentUser _user;
    private readonly IDocumentUrlSigner _signer;

    public DocumentsController(CmsDbContext db, IDocumentStorage storage, ICurrentUser user, IDocumentUrlSigner signer)
    {
        _db = db;
        _storage = storage;
        _user = user;
        _signer = signer;
    }

    public record DocumentDto(
        Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
        string Sha256, string? Category, string? Description,
        string? UploaderName, DateTimeOffset UploadedAtUtc,
        IReadOnlyList<OwnerRef> Owners);

    public record OwnerRef(DocumentOwnerType OwnerType, Guid OwnerId);

    public class UploadForm
    {
        public IFormFile? File { get; set; }
        public DocumentOwnerType OwnerType { get; set; }
        public Guid OwnerId { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
    }

    // --- Upload ---

    [HttpPost("upload")]
    [HasPermission(Permissions.Documents.Write)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes + 4096)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return UnprocessableEntity(new { error = "empty_file" });
        if (form.File.Length > MaxUploadBytes)
            return UnprocessableEntity(new { error = "file_too_large", maxBytes = MaxUploadBytes });
        if (!AllowedContentTypes.Contains(form.File.ContentType))
            return UnprocessableEntity(new { error = "unsupported_content_type", contentType = form.File.ContentType });
        if (form.OwnerId == Guid.Empty)
            return UnprocessableEntity(new { error = "owner_required" });

        if (!await OwnerExistsAsync(form.OwnerType, form.OwnerId, ct))
            return UnprocessableEntity(new { error = "unknown_owner", ownerType = form.OwnerType, ownerId = form.OwnerId });

        // Hash the stream once (dedupe + storage key both derive from this).
        string sha256;
        await using (var hashBuf = new MemoryStream())
        {
            await form.File.CopyToAsync(hashBuf, ct);
            hashBuf.Position = 0;
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(hashBuf, ct);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
            hashBuf.Position = 0;

            var storageKey = BuildStorageKey(sha256, form.File.FileName);
            await _storage.SaveAsync(storageKey, hashBuf, ct);

            var doc = new Document(
                originalFileName: form.File.FileName,
                storageKey: storageKey,
                contentType: form.File.ContentType,
                sizeBytes: form.File.Length,
                sha256: sha256,
                category: form.Category,
                description: form.Description,
                uploaderUserId: _user.UserId,
                uploaderName: _user.UserName);
            _db.Documents.Add(doc);
            _db.DocumentLinks.Add(new DocumentLink(doc.Id, form.OwnerType, form.OwnerId));
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(Get), new { id = doc.Id, version = "1.0" },
                await MapAsync(doc, ct));
        }
    }

    // --- Attach existing (dedup for same owner is enforced by unique index) ---

    public record AttachRequest(Guid DocumentId, DocumentOwnerType OwnerType, Guid OwnerId);

    [HttpPost("attach")]
    [HasPermission(Permissions.Documents.Write)]
    public async Task<ActionResult<DocumentDto>> Attach([FromBody] AttachRequest req, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == req.DocumentId && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (!await OwnerExistsAsync(req.OwnerType, req.OwnerId, ct))
            return UnprocessableEntity(new { error = "unknown_owner" });

        var exists = await _db.DocumentLinks.AnyAsync(
            l => l.DocumentId == req.DocumentId && l.OwnerType == req.OwnerType && l.OwnerId == req.OwnerId, ct);
        if (!exists)
        {
            _db.DocumentLinks.Add(new DocumentLink(req.DocumentId, req.OwnerType, req.OwnerId));
            await _db.SaveChangesAsync(ct);
        }
        return await MapAsync(doc, ct);
    }

    // --- Get single ---

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        return await MapAsync(doc, ct);
    }

    // --- List by owner ---

    [HttpGet]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(
        [FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId, CancellationToken ct)
    {
        if (ownerId == Guid.Empty) return UnprocessableEntity(new { error = "owner_required" });

        var docIds = await _db.DocumentLinks.AsNoTracking()
            .Where(l => l.OwnerType == ownerType && l.OwnerId == ownerId)
            .Select(l => l.DocumentId).ToListAsync(ct);
        if (docIds.Count == 0) return new List<DocumentDto>();

        var docs = await _db.Documents.AsNoTracking()
            .Where(d => docIds.Contains(d.Id) && !d.IsDeleted)
            .ToListAsync(ct);
        var ordered = docs.OrderByDescending(d => d.CreatedAtUtc).ToList();

        var allLinks = await _db.DocumentLinks.AsNoTracking()
            .Where(l => docIds.Contains(l.DocumentId))
            .Select(l => new { l.DocumentId, l.OwnerType, l.OwnerId })
            .ToListAsync(ct);
        var linksByDoc = allLinks.GroupBy(x => x.DocumentId)
            .ToDictionary(g => g.Key,
                          g => (IReadOnlyList<OwnerRef>)g.Select(x => new OwnerRef(x.OwnerType, x.OwnerId)).ToList());

        return ordered.Select(d => ToDto(d, linksByDoc.GetValueOrDefault(d.Id, Array.Empty<OwnerRef>()))).ToList();
    }

    // --- Download ---

    [HttpGet("{id:guid}/download")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (!_storage.Exists(doc.StorageKey))
            return StatusCode(StatusCodes.Status410Gone, new { error = "blob_missing" });
        var stream = await _storage.OpenReadAsync(doc.StorageKey, ct);
        return File(stream, doc.ContentType, doc.OriginalFileName, enableRangeProcessing: true);
    }

    // --- Soft-delete the row (keeps blob for anti-ransom / forensic recovery) ---

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Documents.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();
        doc.SoftDelete(_user.UserId);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Detach a link (does NOT delete the document itself) ---

    [HttpDelete("{id:guid}/owners")]
    [HasPermission(Permissions.Documents.Write)]
    public async Task<IActionResult> Detach(Guid id,
        [FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId, CancellationToken ct)
    {
        var link = await _db.DocumentLinks.FirstOrDefaultAsync(
            l => l.DocumentId == id && l.OwnerType == ownerType && l.OwnerId == ownerId, ct);
        if (link is null) return NotFound();
        _db.DocumentLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- Signed anonymous URLs ---

    public record SignedUrlResponse(string Url, DateTimeOffset ExpiresAtUtc);

    [HttpGet("{id:guid}/signed-url")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<ActionResult<SignedUrlResponse>> SignedUrl(Guid id,
        [FromQuery] int ttlSeconds = 600,
        [FromQuery] Guid? versionId = null,
        CancellationToken ct = default)
    {
        if (ttlSeconds < 30) ttlSeconds = 30;
        if (ttlSeconds > 3600) ttlSeconds = 3600;

        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (versionId.HasValue)
        {
            var versionExists = await _db.DocumentVersions.AnyAsync(v => v.Id == versionId && v.DocumentId == id, ct);
            if (!versionExists) return NotFound(new { error = "unknown_version" });
        }

        var parts = await _signer.IssueAsync(id, versionId, TimeSpan.FromSeconds(ttlSeconds), ct);
        var qs = $"exp={parts.ExpUnix}&sig={parts.SigBase64Url}" + (versionId.HasValue ? $"&v={versionId:N}" : "");
        var url = $"/api/v1/documents/signed/{id}?{qs}";
        return new SignedUrlResponse(url, parts.ExpiresAtUtc);
    }

    [HttpGet("signed/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> SignedDownload(Guid id,
        [FromQuery] long exp,
        [FromQuery] string sig,
        [FromQuery(Name = "v")] Guid? versionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sig)) return StatusCode(StatusCodes.Status403Forbidden, new { error = "missing_sig" });
        var ok = await _signer.VerifyAsync(id, versionId, exp, sig, ct);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { error = "invalid_or_expired_signature" });

        if (versionId.HasValue)
        {
            var v = await _db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.DocumentId == id, ct);
            if (v is null) return NotFound();
            if (!_storage.Exists(v.StorageKey)) return StatusCode(StatusCodes.Status410Gone, new { error = "blob_missing" });
            var stream = await _storage.OpenReadAsync(v.StorageKey, ct);
            return File(stream, v.ContentType, v.OriginalFileName, enableRangeProcessing: true);
        }

        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (!_storage.Exists(doc.StorageKey)) return StatusCode(StatusCodes.Status410Gone, new { error = "blob_missing" });
        var docStream = await _storage.OpenReadAsync(doc.StorageKey, ct);
        return File(docStream, doc.ContentType, doc.OriginalFileName, enableRangeProcessing: true);
    }

    // --- Versions ---

    public record DocumentVersionDto(Guid Id, int VersionNumber, string OriginalFileName,
                                       string ContentType, long SizeBytes, string Sha256,
                                       string? UploaderName, DateTimeOffset UploadedAtUtc);

    public class VersionUploadForm
    {
        public IFormFile? File { get; set; }
    }

    [HttpPost("{id:guid}/versions")]
    [HasPermission(Permissions.Documents.Write)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes + 4096)]
    public async Task<ActionResult<DocumentVersionDto>> UploadVersion(Guid id, [FromForm] VersionUploadForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return UnprocessableEntity(new { error = "empty_file" });
        if (form.File.Length > MaxUploadBytes)
            return UnprocessableEntity(new { error = "file_too_large", maxBytes = MaxUploadBytes });
        if (!AllowedContentTypes.Contains(form.File.ContentType))
            return UnprocessableEntity(new { error = "unsupported_content_type", contentType = form.File.ContentType });

        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();

        string sha256;
        await using var hashBuf = new MemoryStream();
        await form.File.CopyToAsync(hashBuf, ct);
        hashBuf.Position = 0;
        using (var sha = SHA256.Create())
        {
            var hashBytes = await sha.ComputeHashAsync(hashBuf, ct);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        hashBuf.Position = 0;

        var newStorageKey = BuildStorageKey(sha256, form.File.FileName);
        await _storage.SaveAsync(newStorageKey, hashBuf, ct);

        // Archive current as a numbered version row.
        var nextNumber = 1 + await _db.DocumentVersions.CountAsync(v => v.DocumentId == doc.Id, ct);
        var archive = new DocumentVersion(
            documentId: doc.Id,
            versionNumber: nextNumber,
            originalFileName: doc.OriginalFileName,
            storageKey: doc.StorageKey,
            contentType: doc.ContentType,
            sizeBytes: doc.SizeBytes,
            sha256: doc.Sha256,
            uploaderUserId: doc.UploaderUserId,
            uploaderName: doc.UploaderName);
        _db.DocumentVersions.Add(archive);

        doc.ReplaceCurrent(
            originalFileName: form.File.FileName,
            storageKey: newStorageKey,
            contentType: form.File.ContentType,
            sizeBytes: form.File.Length,
            sha256: sha256,
            uploaderUserId: _user.UserId,
            uploaderName: _user.UserName);

        await _db.SaveChangesAsync(ct);
        return ToVersionDto(archive);
    }

    [HttpGet("{id:guid}/versions")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<ActionResult<IReadOnlyList<DocumentVersionDto>>> ListVersions(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        var versions = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == id)
            .ToListAsync(ct);
        return versions.OrderByDescending(v => v.VersionNumber).Select(ToVersionDto).ToList();
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<IActionResult> DownloadVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        var v = await _db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.DocumentId == id, ct);
        if (v is null) return NotFound();
        if (!_storage.Exists(v.StorageKey)) return StatusCode(StatusCodes.Status410Gone, new { error = "blob_missing" });
        var stream = await _storage.OpenReadAsync(v.StorageKey, ct);
        return File(stream, v.ContentType, v.OriginalFileName, enableRangeProcessing: true);
    }

    // --- Approvals ---

    public record DocumentApprovalDto(Guid Id, Guid DocumentId, DocumentApprovalStatus Status,
                                       Guid RequesterUserId, string? RequesterName, string? RequesterNote,
                                       Guid? ApproverUserId, string? ApproverName, DateTimeOffset? DecidedAtUtc,
                                       string? DecisionNote, DateTimeOffset CreatedAtUtc);

    public record RequestApprovalRequest(string? Note);
    public record ApprovalDecisionRequest(string? Note);

    [HttpPost("{id:guid}/approvals")]
    [HasPermission(Permissions.Documents.Write)]
    public async Task<ActionResult<DocumentApprovalDto>> RequestApproval(Guid id, [FromBody] RequestApprovalRequest req, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (_user.UserId is null) return Unauthorized();

        var hasPending = await _db.DocumentApprovals.AnyAsync(a => a.DocumentId == id && a.Status == DocumentApprovalStatus.Pending, ct);
        if (hasPending) return Conflict(new { error = "approval_pending_exists" });

        var approval = new DocumentApproval(id, _user.UserId.Value, _user.UserName, req.Note);
        _db.DocumentApprovals.Add(approval);
        await _db.SaveChangesAsync(ct);
        return ToApprovalDto(approval);
    }

    [HttpGet("{id:guid}/approvals")]
    [HasPermission(Permissions.Documents.Read)]
    public async Task<ActionResult<IReadOnlyList<DocumentApprovalDto>>> ListApprovals(Guid id, CancellationToken ct)
    {
        var items = await _db.DocumentApprovals.AsNoTracking().Where(a => a.DocumentId == id).ToListAsync(ct);
        return items.OrderByDescending(a => a.CreatedAtUtc).Select(ToApprovalDto).ToList();
    }

    [HttpPost("{id:guid}/approvals/{approvalId:guid}/approve")]
    [HasPermission(Permissions.Documents.Approve)]
    public Task<IActionResult> Approve(Guid id, Guid approvalId, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
        => DecideAsync(id, approvalId, DocumentApprovalStatus.Approved, req.Note, ct);

    [HttpPost("{id:guid}/approvals/{approvalId:guid}/reject")]
    [HasPermission(Permissions.Documents.Approve)]
    public Task<IActionResult> Reject(Guid id, Guid approvalId, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
        => DecideAsync(id, approvalId, DocumentApprovalStatus.Rejected, req.Note, ct);

    private async Task<IActionResult> DecideAsync(Guid id, Guid approvalId, DocumentApprovalStatus decision, string? note, CancellationToken ct)
    {
        var a = await _db.DocumentApprovals.FirstOrDefaultAsync(x => x.Id == approvalId && x.DocumentId == id, ct);
        if (a is null) return NotFound();
        if (_user.UserId is null) return Unauthorized();
        try
        {
            a.Decide(decision, _user.UserId.Value, _user.UserName, note);
            await _db.SaveChangesAsync(ct);
            return Ok(ToApprovalDto(a));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private static DocumentVersionDto ToVersionDto(DocumentVersion v) =>
        new(v.Id, v.VersionNumber, v.OriginalFileName, v.ContentType, v.SizeBytes, v.Sha256, v.UploaderName, v.CreatedAtUtc);

    private static DocumentApprovalDto ToApprovalDto(DocumentApproval a) =>
        new(a.Id, a.DocumentId, a.Status, a.RequesterUserId, a.RequesterName, a.RequesterNote,
            a.ApproverUserId, a.ApproverName, a.DecidedAtUtc, a.DecisionNote, a.CreatedAtUtc);

    // --- helpers ---

    private async Task<bool> OwnerExistsAsync(DocumentOwnerType t, Guid id, CancellationToken ct) => t switch
    {
        DocumentOwnerType.Client => await _db.Clients.AnyAsync(x => x.Id == id, ct),
        DocumentOwnerType.Branch => await _db.Branches.AnyAsync(x => x.Id == id, ct),
        DocumentOwnerType.ServiceFile => await _db.ServiceFiles.AnyAsync(x => x.Id == id, ct),
        DocumentOwnerType.ComplianceObligation => await _db.ComplianceObligations.AnyAsync(x => x.Id == id, ct),
        DocumentOwnerType.Engagement => await _db.Engagements.AnyAsync(x => x.Id == id, ct),
        _ => false,
    };

    private static string BuildStorageKey(string sha256, string originalFileName)
    {
        // Shard by first two hex chars to keep directories small.
        var ext = Path.GetExtension(originalFileName);
        if (!string.IsNullOrEmpty(ext) && ext.Length > 10) ext = ""; // defend against pathological extensions
        return Path.Combine(sha256[..2], sha256[2..4], sha256 + ext).Replace('\\', '/');
    }

    private async Task<DocumentDto> MapAsync(Document d, CancellationToken ct)
    {
        var owners = await _db.DocumentLinks.AsNoTracking()
            .Where(l => l.DocumentId == d.Id)
            .Select(l => new OwnerRef(l.OwnerType, l.OwnerId))
            .ToListAsync(ct);
        return ToDto(d, owners);
    }

    private static DocumentDto ToDto(Document d, IReadOnlyList<OwnerRef> owners) =>
        new(d.Id, d.OriginalFileName, d.ContentType, d.SizeBytes,
            d.Sha256, d.Category, d.Description, d.UploaderName, d.CreatedAtUtc, owners);
}
