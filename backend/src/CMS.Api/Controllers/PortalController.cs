using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Application.Billing;
using CMS.Domain.Entities;
using CMS.Infrastructure.Documents;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>
/// Read-only API surface exposed to external client-portal users. Every endpoint enforces that the
/// signed-in user's <c>PortalClientId</c> matches the resource's owner — this is the single
/// horizontal-auth boundary for the portal.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/portal")]
[HasPermission(Permissions.Portal.Access)]
public class PortalController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IInvoicePdfRenderer _pdf;
    private readonly IDocumentStorage _storage;

    public PortalController(CmsDbContext db, ICurrentUser user,
                             IInvoicePdfRenderer pdf, IDocumentStorage storage)
    { _db = db; _user = user; _pdf = pdf; _storage = storage; }

    /// <summary>Resolve the signed-in portal user's client, or return a 403-equivalent.</summary>
    private Guid? ClientIdOrNull() => _user.PortalClientId;

    public record MeDto(Guid ClientId, string Code, string LegalName, string? TradeName, string? Pan);

    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me(CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();
        var row = await _db.Clients.AsNoTracking().Where(c => c.Id == cid)
            .Select(c => new MeDto(c.Id, c.Code, c.LegalName, c.TradeName, c.Pan)).FirstOrDefaultAsync(ct);
        return row is null ? NotFound() : row;
    }

    // ---------- Invoices ----------

    public record PortalInvoiceDto(Guid Id, string Number, string FinancialYear, DateOnly IssueDate, DateOnly DueDate,
                              InvoiceStatus Status, decimal TotalInr, decimal PaidInr, decimal OutstandingInr,
                              EInvoiceStatus EInvoiceStatus);

    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyList<PortalInvoiceDto>>> Invoices(CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();
        var rows = await _db.Invoices.AsNoTracking()
            .Where(i => i.ClientId == cid && i.Status != InvoiceStatus.Draft)
            .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Number)
            .Select(i => new PortalInvoiceDto(i.Id, i.Number, i.FinancialYear, i.IssueDate, i.DueDate,
                i.Status, i.TotalInr, i.PaidInr, i.TotalInr - i.PaidInr, i.EInvoiceStatus))
            .ToListAsync(ct);
        return rows;
    }

    [HttpGet("invoices/{id:guid}/pdf")]
    public async Task<IActionResult> InvoicePdf(Guid id, CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null || invoice.ClientId != cid) return NotFound();
        if (invoice.Status == InvoiceStatus.Draft) return NotFound();

        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == invoice.ClientId, ct);
        var branch = await _db.Branches.AsNoTracking().FirstAsync(b => b.Id == invoice.BranchId, ct);
        var pos = await _db.States.AsNoTracking().FirstAsync(s => s.Id == invoice.PlaceOfSupplyStateId, ct);
        var clientGstin = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => r.BranchId == invoice.BranchId && r.Type == TaxRegistrationType.Gstin && r.IsActive)
            .Select(r => r.Number).FirstOrDefaultAsync(ct);
        var clientAddress = branch.Address is null ? null
            : string.Join(", ", new[] { branch.Address.Line1, branch.Address.Line2,
                                        branch.Address.City, pos.Name, branch.Address.Pincode }
                                  .Where(s => !string.IsNullOrWhiteSpace(s)));

        var lineRows = await _db.InvoiceLineItems.AsNoTracking()
            .Where(l => l.InvoiceId == id).OrderBy(l => l.LineNumber).ToListAsync(ct);

        var model = new InvoicePdfModel(
            invoice.Number, invoice.FinancialYear, invoice.IssueDate, invoice.DueDate, invoice.Status,
            client.LegalName, client.Pan, clientGstin, clientAddress, pos.Name, invoice.IsIntraState,
            lineRows.Select(l => new InvoicePdfLine(
                l.LineNumber, l.Description, null, null, null,
                l.Quantity, l.UnitPriceInr, l.GstRatePercent,
                l.LineSubtotalInr, l.LineGstInr, l.LineTotalInr)).ToList(),
            invoice.SubtotalInr, invoice.CgstInr, invoice.SgstInr, invoice.IgstInr,
            invoice.TotalInr, invoice.PaidInr, Math.Round(invoice.TotalInr - invoice.PaidInr, 2),
            invoice.Notes)
        { Irn = invoice.Irn, AckNo = invoice.AckNo, AckDateUtc = invoice.AckDateUtc, QrText = invoice.QrText };
        var bytes = _pdf.Render(model);
        return File(bytes, "application/pdf", $"{invoice.Number.Replace('/', '-')}.pdf");
    }

    // ---------- Obligations ----------

    public record PortalObligationDto(Guid Id, string PeriodKey, DateOnly PeriodStart, DateOnly PeriodEnd,
                                 DateOnly DueDate, ComplianceStatus Status, DateOnly? FiledOn,
                                 string ServiceCode);

    [HttpGet("obligations")]
    public async Task<ActionResult<IReadOnlyList<PortalObligationDto>>> Obligations(CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();
        var rows = await (from o in _db.ComplianceObligations.AsNoTracking()
                          join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                          where f.ClientId == cid
                          join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                          orderby o.DueDate descending
                          select new PortalObligationDto(o.Id, o.PeriodKey, o.PeriodStart, o.PeriodEnd,
                              o.DueDate, o.Status, o.FiledOn, s.Code)).ToListAsync(ct);
        return rows;
    }

    // ---------- Documents ----------

    public record PortalDocumentDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
                               string? Category, string? Description, DateTimeOffset UploadedAtUtc);

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<PortalDocumentDto>>> Documents(CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();

        // A document is visible to the portal user iff at least one DocumentLink points at an
        // entity owned by this client (Client / Branch / ServiceFile / Obligation / Engagement).
        var clientLinked = _db.DocumentLinks.AsNoTracking()
            .Where(l => l.OwnerType == DocumentOwnerType.Client && l.OwnerId == cid);
        var branchIds = _db.Branches.AsNoTracking().Where(b => b.ClientId == cid).Select(b => b.Id);
        var branchLinked = _db.DocumentLinks.AsNoTracking()
            .Where(l => l.OwnerType == DocumentOwnerType.Branch && branchIds.Contains(l.OwnerId));
        var fileIds = _db.ServiceFiles.AsNoTracking().Where(f => f.ClientId == cid).Select(f => f.Id);
        var fileLinked = _db.DocumentLinks.AsNoTracking()
            .Where(l => l.OwnerType == DocumentOwnerType.ServiceFile && fileIds.Contains(l.OwnerId));

        var docIds = await clientLinked.Concat(branchLinked).Concat(fileLinked)
            .Select(l => l.DocumentId).Distinct().ToListAsync(ct);
        if (docIds.Count == 0) return new List<PortalDocumentDto>();

        return await _db.Documents.AsNoTracking()
            .Where(d => docIds.Contains(d.Id) && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new PortalDocumentDto(d.Id, d.OriginalFileName, d.ContentType, d.SizeBytes,
                d.Category, d.Description, d.CreatedAtUtc))
            .ToListAsync(ct);
    }

    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DocumentDownload(Guid id, CancellationToken ct)
    {
        var cid = ClientIdOrNull();
        if (cid is null) return Forbid();

        // Reuse the visibility logic from the list endpoint for the download gate.
        var ok = await DocumentIsVisibleAsync(id, cid.Value, ct);
        if (!ok) return NotFound();

        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound();
        if (!_storage.Exists(doc.StorageKey)) return StatusCode(StatusCodes.Status410Gone, new { error = "blob_missing" });
        var stream = await _storage.OpenReadAsync(doc.StorageKey, ct);
        return File(stream, doc.ContentType, doc.OriginalFileName, enableRangeProcessing: true);
    }

    private async Task<bool> DocumentIsVisibleAsync(Guid docId, Guid cid, CancellationToken ct)
    {
        var branchIds = await _db.Branches.AsNoTracking().Where(b => b.ClientId == cid).Select(b => b.Id).ToListAsync(ct);
        var fileIds = await _db.ServiceFiles.AsNoTracking().Where(f => f.ClientId == cid).Select(f => f.Id).ToListAsync(ct);
        return await _db.DocumentLinks.AsNoTracking().AnyAsync(l => l.DocumentId == docId &&
            (
                (l.OwnerType == DocumentOwnerType.Client && l.OwnerId == cid) ||
                (l.OwnerType == DocumentOwnerType.Branch && branchIds.Contains(l.OwnerId)) ||
                (l.OwnerType == DocumentOwnerType.ServiceFile && fileIds.Contains(l.OwnerId))
            ), ct);
    }
}
