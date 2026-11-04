using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Application.Billing;
using CMS.Application.Email;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly IInvoiceNumberGenerator _numberGen;
    private readonly IInvoicePdfRenderer _pdf;
    private readonly IEmailSender _emailSender;
    private readonly IEInvoiceProvider _eInvoice;
    private readonly IInvoiceSigner _signer;
    private readonly ISettingsStore _settings;
    private readonly CMS.Application.Abstractions.ICurrentUser _user;
    public InvoicesController(CmsDbContext db, IInvoiceNumberGenerator numberGen, IInvoicePdfRenderer pdf,
                               IEmailSender emailSender, IEInvoiceProvider eInvoice,
                               IInvoiceSigner signer, ISettingsStore settings,
                               CMS.Application.Abstractions.ICurrentUser user)
    { _db = db; _numberGen = numberGen; _pdf = pdf; _emailSender = emailSender; _eInvoice = eInvoice; _signer = signer; _settings = settings; _user = user; }

    public record LineItemDto(
        Guid Id, int LineNumber, string Description,
        Guid? ServiceDefinitionId, string? ServiceCode,
        Guid? ServiceFileId, string? FileNumber,
        Guid? ComplianceObligationId, string? ObligationPeriodKey,
        decimal Quantity, decimal UnitPriceInr, decimal GstRatePercent,
        decimal LineSubtotalInr, decimal LineGstInr, decimal LineTotalInr);

    public record InvoiceDto(
        Guid Id, string Number, string FinancialYear,
        Guid ClientId, string ClientCode, string ClientName,
        Guid BranchId, string BranchCode, string BranchName,
        Guid PlaceOfSupplyStateId, string PlaceOfSupplyStateName,
        InvoiceStatus Status, DateOnly IssueDate, DateOnly DueDate, string? Notes,
        bool IsIntraState,
        decimal SubtotalInr, decimal CgstInr, decimal SgstInr, decimal IgstInr, decimal TotalInr,
        decimal PaidInr, decimal CreditedInr, decimal OutstandingInr,
        IReadOnlyList<LineItemDto> LineItems, int Version,
        EInvoiceStatus EInvoiceStatus, string? Irn, string? AckNo, DateTimeOffset? AckDateUtc);

    public record LineItemRequest(
        [Required, StringLength(500)] string Description,
        Guid? ServiceDefinitionId, Guid? ServiceFileId, Guid? ComplianceObligationId,
        [Range(0.001, 1_000_000)] decimal Quantity,
        [Range(0, 10_000_000)] decimal UnitPriceInr,
        [Range(0, 100)] decimal GstRatePercent);

    public record CreateInvoiceRequest(
        [Required] Guid ClientId,
        [Required] Guid BranchId,
        [Required] Guid PlaceOfSupplyStateId,
        [Required] DateOnly IssueDate,
        [Required] DateOnly DueDate,
        [StringLength(2000)] string? Notes,
        [Required, MinLength(1)] List<LineItemRequest> LineItems);

    public record UpdateInvoiceRequest(
        [Required] DateOnly IssueDate,
        [Required] DateOnly DueDate,
        [StringLength(2000)] string? Notes,
        List<LineItemRequest> LineItems);

    public record InvoicePage<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
    public record InvoiceAggregates(decimal OutstandingInr, decimal OverdueInr, decimal RevenueThisFyInr,
                                     int Draft, int Issued, int PartPaid, int Paid, int Cancelled);

    // ----- List + aggregates -----

    [HttpGet]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<InvoicePage<InvoiceDto>>> List(
        [FromQuery] InvoiceStatus? status, [FromQuery] Guid? clientId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var q = _db.Invoices.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(i => i.Status == status);
        if (clientId is not null) q = q.Where(i => i.ClientId == clientId);
        if (from is not null) q = q.Where(i => i.IssueDate >= from);
        if (to is not null) q = q.Where(i => i.IssueDate <= to);
        var total = await q.CountAsync(ct);

        var rows = await q.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Number)
            .Skip(skip).Take(take).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToArray();
        var items = await LoadDtosAsync(ids, ct);
        var ordered = items.OrderBy(d => rows.FindIndex(r => r.Id == d.Id)).ToList();
        return new InvoicePage<InvoiceDto>(ordered, total, skip, take);
    }

    [HttpGet("aggregates")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<InvoiceAggregates>> GetAggregates(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fy = InvoiceNumberGenerator.FinancialYearFor(today);

        var all = await _db.Invoices.AsNoTracking()
            .Select(i => new { i.Status, i.TotalInr, i.PaidInr, i.CreditedInr, i.DueDate, i.FinancialYear })
            .ToListAsync(ct);

        decimal outstanding = all
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartPaid)
            .Sum(i => i.TotalInr - i.PaidInr - i.CreditedInr);
        decimal overdue = all
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartPaid && i.DueDate < today)
            .Sum(i => i.TotalInr - i.PaidInr - i.CreditedInr);
        decimal revenueFy = all
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft && i.FinancialYear == fy)
            .Sum(i => i.TotalInr);
        return new InvoiceAggregates(
            Math.Round(outstanding, 2),
            Math.Round(overdue, 2),
            Math.Round(revenueFy, 2),
            all.Count(i => i.Status == InvoiceStatus.Draft),
            all.Count(i => i.Status == InvoiceStatus.Issued),
            all.Count(i => i.Status == InvoiceStatus.PartPaid),
            all.Count(i => i.Status == InvoiceStatus.Paid),
            all.Count(i => i.Status == InvoiceStatus.Cancelled));
    }

    // ----- Single -----

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id, CancellationToken ct)
    {
        var one = (await LoadDtosAsync(new[] { id }, ct)).FirstOrDefault();
        return one is null ? NotFound() : one;
    }

    // ----- Create (Draft) -----

    [HttpPost]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) return UnprocessableEntity(new { error = "unknown_client" });
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == req.BranchId && b.ClientId == req.ClientId, ct);
        if (branch is null) return UnprocessableEntity(new { error = "unknown_or_mismatched_branch" });
        if (!await _db.States.AnyAsync(s => s.Id == req.PlaceOfSupplyStateId, ct))
            return UnprocessableEntity(new { error = "unknown_state" });

        // Determine intra-state based on billing branch's state vs place of supply.
        var billingStateId = branch.StateId;
        var isIntraState = billingStateId == req.PlaceOfSupplyStateId;

        var fy = InvoiceNumberGenerator.FinancialYearFor(req.IssueDate);
        var number = await _numberGen.NextAsync(fy, ct);

        var invoice = new Invoice(number, fy, req.ClientId, req.BranchId, req.PlaceOfSupplyStateId,
            req.IssueDate, req.DueDate, isIntraState, req.Notes);

        int lineNumber = 1;
        foreach (var li in req.LineItems)
        {
            var error = await ValidateLineReferencesAsync(li, ct);
            if (error is not null) return UnprocessableEntity(new { error });
            invoice.AddLineItem(new InvoiceLineItem(invoice.Id, lineNumber++, li.Description,
                li.ServiceDefinitionId, li.ServiceFileId, li.ComplianceObligationId,
                li.Quantity, li.UnitPriceInr, li.GstRatePercent));
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        var dto = (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
        return CreatedAtAction(nameof(Get), new { id = invoice.Id, version = "1.0" }, dto);
    }

    // ----- Update draft lines/dates -----

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> Update(Guid id, [FromBody] UpdateInvoiceRequest req, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.LineItems).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        try
        {
            invoice.UpdateProfile(req.IssueDate, req.DueDate, req.Notes);
            invoice.ClearLineItems();
            int lineNumber = 1;
            foreach (var li in req.LineItems)
            {
                var error = await ValidateLineReferencesAsync(li, ct);
                if (error is not null) return UnprocessableEntity(new { error });
                invoice.AddLineItem(new InvoiceLineItem(invoice.Id, lineNumber++, li.Description,
                    li.ServiceDefinitionId, li.ServiceFileId, li.ComplianceObligationId,
                    li.Quantity, li.UnitPriceInr, li.GstRatePercent));
            }
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "not_editable", detail = ex.Message }); }

        await _db.SaveChangesAsync(ct);
        return (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
    }

    // ----- Issue / Cancel -----

    [HttpPost("{id:guid}/issue")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> Issue(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.LineItems).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        try { invoice.Issue(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "cannot_issue", detail = ex.Message }); }
        await _db.SaveChangesAsync(ct);
        return (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> Cancel(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        try { invoice.Cancel(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "cannot_cancel", detail = ex.Message }); }
        await _db.SaveChangesAsync(ct);
        return (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
    }

    // ----- Payments -----

    public record PaymentDto(Guid Id, Guid InvoiceId, DateOnly PaidOn, decimal AmountInr,
                             PaymentMode Mode, string? Reference, string? Notes, bool IsReversed);
    public record RecordPaymentRequest(
        [Required] DateOnly PaidOn,
        [Range(0.01, 10_000_000)] decimal AmountInr,
        [Required] PaymentMode Mode,
        [StringLength(100)] string? Reference,
        [StringLength(2000)] string? Notes);

    [HttpGet("{id:guid}/payments")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListPayments(Guid id, CancellationToken ct)
    {
        var rows = await _db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == id)
            .Select(p => new PaymentDto(p.Id, p.InvoiceId, p.PaidOn, p.AmountInr, p.Mode, p.Reference, p.Notes, p.IsReversed))
            .ToListAsync(ct);
        return rows.OrderByDescending(p => p.PaidOn).ToList();
    }

    [HttpPost("{id:guid}/payments")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> RecordPayment(Guid id, [FromBody] RecordPaymentRequest req, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        try { invoice.RecordPayment(req.AmountInr); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_payment", detail = ex.Message }); }

        _db.Payments.Add(new Payment(invoice.Id, req.PaidOn, req.AmountInr, req.Mode, req.Reference, req.Notes));
        await _db.SaveChangesAsync(ct);
        return (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
    }

    [HttpPost("{id:guid}/payments/{paymentId:guid}/reverse")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<InvoiceDto>> ReversePayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.InvoiceId == id, ct);
        if (payment is null || payment.IsReversed) return NotFound();
        invoice.ReversePayment(payment.AmountInr);
        payment.Reverse();
        await _db.SaveChangesAsync(ct);
        return (await LoadDtosAsync(new[] { invoice.Id }, ct)).First();
    }

    // ----- PDF -----

    [HttpGet("{id:guid}/pdf")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        var client = await _db.Clients.AsNoTracking().FirstAsync(c => c.Id == invoice.ClientId, ct);
        var branch = await _db.Branches.AsNoTracking().FirstAsync(b => b.Id == invoice.BranchId, ct);
        var pos = await _db.States.AsNoTracking().FirstAsync(s => s.Id == invoice.PlaceOfSupplyStateId, ct);

        // Resolve the client-side GSTIN on their billing branch, if we captured one in tax_registrations.
        var clientGstin = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => r.BranchId == invoice.BranchId
                        && r.Type == TaxRegistrationType.Gstin && r.IsActive)
            .Select(r => r.Number).FirstOrDefaultAsync(ct);
        var clientAddress = branch.Address is null
            ? null
            : string.Join(", ", new[] { branch.Address.Line1, branch.Address.Line2,
                                        branch.Address.City, pos.Name, branch.Address.Pincode }
                                  .Where(s => !string.IsNullOrWhiteSpace(s)));

        var lineRows = await (from l in _db.InvoiceLineItems.AsNoTracking()
                              where l.InvoiceId == id
                              join s in _db.ServiceDefinitions.AsNoTracking() on l.ServiceDefinitionId equals s.Id into sj
                              from s in sj.DefaultIfEmpty()
                              join sf in _db.ServiceFiles.AsNoTracking() on l.ServiceFileId equals sf.Id into sfj
                              from sf in sfj.DefaultIfEmpty()
                              join ob in _db.ComplianceObligations.AsNoTracking() on l.ComplianceObligationId equals ob.Id into obj
                              from ob in obj.DefaultIfEmpty()
                              orderby l.LineNumber
                              select new { l, sCode = s == null ? null : s.Code,
                                            fileNumber = sf == null ? null : sf.FileNumber,
                                            periodKey = ob == null ? null : ob.PeriodKey }).ToListAsync(ct);

        var model = new InvoicePdfModel(
            Number: invoice.Number,
            FinancialYear: invoice.FinancialYear,
            IssueDate: invoice.IssueDate,
            DueDate: invoice.DueDate,
            Status: invoice.Status,
            ClientName: client.LegalName,
            ClientPan: client.Pan,
            ClientGstin: clientGstin,
            ClientAddress: clientAddress,
            PlaceOfSupplyState: pos.Name,
            IsIntraState: invoice.IsIntraState,
            Lines: lineRows.Select(r => new InvoicePdfLine(
                r.l.LineNumber, r.l.Description, r.sCode, r.fileNumber, r.periodKey,
                r.l.Quantity, r.l.UnitPriceInr, r.l.GstRatePercent,
                r.l.LineSubtotalInr, r.l.LineGstInr, r.l.LineTotalInr)).ToList(),
            SubtotalInr: invoice.SubtotalInr,
            CgstInr: invoice.CgstInr,
            SgstInr: invoice.SgstInr,
            IgstInr: invoice.IgstInr,
            TotalInr: invoice.TotalInr,
            PaidInr: invoice.PaidInr,
            OutstandingInr: Math.Round(invoice.TotalInr - invoice.PaidInr, 2),
            Notes: invoice.Notes);

        // First render — raw bytes feed the signer; if a signature is produced we re-render with
        // the visible signature stamp so the final PDF matches what the detached .p7s was signed over
        // (the detached CMS covers the UNSIGNED bytes — this is a visible-stamp convention, not an
        // inline PAdES signature).
        var baseModel = model with { Irn = invoice.Irn, AckNo = invoice.AckNo,
            AckDateUtc = invoice.AckDateUtc, QrText = invoice.QrText };
        var rawBytes = _pdf.Render(baseModel);
        var signature = await _signer.SignAsync(rawBytes, ct);
        var finalBytes = signature is null
            ? rawBytes
            : _pdf.Render(baseModel with {
                SignerName = signature.SignerName,
                SignatureThumbprint = signature.CertificateThumbprint,
                SignedAtUtc = signature.SignedAtUtc,
            });
        var filename = $"{invoice.Number.Replace('/', '-')}.pdf";
        if (signature is not null)
            Response.Headers.Append("X-Signature-Available", "true");
        return File(finalBytes, "application/pdf", filename);
    }

    [HttpGet("{id:guid}/pdf/signature")]
    [HasPermission(Permissions.Billing.Read)]
    public async Task<IActionResult> DownloadPdfSignature(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        var model = await BuildPdfModelAsync(id, ct);
        if (model is null) return NotFound();
        var rawBytes = _pdf.Render(model);
        var sig = await _signer.SignAsync(rawBytes, ct);
        if (sig?.DetachedCms is null)
            return StatusCode(StatusCodes.Status409Conflict, new { error = "signature_not_configured" });
        return File(sig.DetachedCms, "application/pkcs7-signature",
            $"{invoice.Number.Replace('/', '-')}.p7s");
    }

    // ----- E-invoicing (IRN / QR) -----

    public record GenerateIrnResponse(EInvoiceStatus Status, string? Irn, string? AckNo, DateTimeOffset? AckDateUtc);
    public record CancelIrnRequest([System.ComponentModel.DataAnnotations.StringLength(500)] string? Reason);

    [HttpPost("{id:guid}/einvoice/generate")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<GenerateIrnResponse>> GenerateIrn(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        if (invoice.EInvoiceStatus == EInvoiceStatus.Generated)
            return UnprocessableEntity(new { error = "already_generated" });

        var pdfResult = await BuildPdfModelAsync(id, ct);
        if (pdfResult is null) return NotFound();

        try
        {
            var r = await _eInvoice.GenerateAsync(pdfResult, ct);
            invoice.MarkEInvoiced(r.Irn, r.AckNo, r.AckDateUtc, r.QrText);
            await _db.SaveChangesAsync(ct);
            return new GenerateIrnResponse(invoice.EInvoiceStatus, invoice.Irn, invoice.AckNo, invoice.AckDateUtc);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_transition", detail = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(StatusCodes.Status501NotImplemented, new { error = "provider_not_configured", detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/einvoice/cancel")]
    [HasPermission(Permissions.Billing.Write)]
    public async Task<ActionResult<GenerateIrnResponse>> CancelIrn(Guid id, [FromBody] CancelIrnRequest req, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        if (invoice.EInvoiceStatus != EInvoiceStatus.Generated || string.IsNullOrWhiteSpace(invoice.Irn))
            return UnprocessableEntity(new { error = "no_active_irn" });

        try
        {
            var ok = await _eInvoice.CancelAsync(invoice.Irn, req.Reason ?? "unspecified", ct);
            if (!ok) return StatusCode(StatusCodes.Status502BadGateway, new { error = "irp_cancel_rejected" });
            invoice.CancelEInvoice(req.Reason ?? "unspecified");
            await _db.SaveChangesAsync(ct);
            return new GenerateIrnResponse(invoice.EInvoiceStatus, invoice.Irn, invoice.AckNo, invoice.AckDateUtc);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_transition", detail = ex.Message }); }
        catch (NotImplementedException ex) { return StatusCode(StatusCodes.Status501NotImplemented, new { error = "provider_not_configured", detail = ex.Message }); }
    }

    /// <summary>Build the PDF model used both for /pdf download and for the e-invoice payload.</summary>
    private async Task<InvoicePdfModel?> BuildPdfModelAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return null;
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

        var lineRows = await (from l in _db.InvoiceLineItems.AsNoTracking()
                              where l.InvoiceId == id
                              join s in _db.ServiceDefinitions.AsNoTracking() on l.ServiceDefinitionId equals s.Id into sj
                              from s in sj.DefaultIfEmpty()
                              join sf in _db.ServiceFiles.AsNoTracking() on l.ServiceFileId equals sf.Id into sfj
                              from sf in sfj.DefaultIfEmpty()
                              join ob in _db.ComplianceObligations.AsNoTracking() on l.ComplianceObligationId equals ob.Id into obj
                              from ob in obj.DefaultIfEmpty()
                              orderby l.LineNumber
                              select new { l, sCode = s == null ? null : s.Code,
                                            fileNumber = sf == null ? null : sf.FileNumber,
                                            periodKey = ob == null ? null : ob.PeriodKey }).ToListAsync(ct);

        return new InvoicePdfModel(
            invoice.Number, invoice.FinancialYear, invoice.IssueDate, invoice.DueDate, invoice.Status,
            client.LegalName, client.Pan, clientGstin, clientAddress, pos.Name, invoice.IsIntraState,
            lineRows.Select(r => new InvoicePdfLine(
                r.l.LineNumber, r.l.Description, r.sCode, r.fileNumber, r.periodKey,
                r.l.Quantity, r.l.UnitPriceInr, r.l.GstRatePercent,
                r.l.LineSubtotalInr, r.l.LineGstInr, r.l.LineTotalInr)).ToList(),
            invoice.SubtotalInr, invoice.CgstInr, invoice.SgstInr, invoice.IgstInr,
            invoice.TotalInr, invoice.PaidInr, Math.Round(invoice.TotalInr - invoice.PaidInr, 2),
            invoice.Notes)
        { Irn = invoice.Irn, AckNo = invoice.AckNo, AckDateUtc = invoice.AckDateUtc, QrText = invoice.QrText };
    }

    // ----- Email -----

    public record SendEmailRequest(
        [System.ComponentModel.DataAnnotations.MinLength(1)] List<string> To,
        List<string>? Cc,
        [System.ComponentModel.DataAnnotations.StringLength(500)] string? Subject,
        [System.ComponentModel.DataAnnotations.StringLength(10000)] string? Body);

    public record SendEmailResponse(Guid OutboxId, EmailStatus Status, string? ErrorMessage);

    [HttpPost("{id:guid}/send-email")]
    [HasPermission(Permissions.Email.Send)]
    public async Task<ActionResult<SendEmailResponse>> SendEmail(Guid id, [FromBody] SendEmailRequest req, CancellationToken ct)
    {
        if (req.To is null || req.To.Count == 0 || req.To.All(string.IsNullOrWhiteSpace))
            return UnprocessableEntity(new { error = "to_required" });

        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        var pdfResult = await BuildPdfBytesAsync(id, ct);
        if (pdfResult is null) return NotFound();
        var (pdfBytes, pdfFileName) = pdfResult.Value;

        var toList = req.To.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        var ccList = (req.Cc ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        var subject = string.IsNullOrWhiteSpace(req.Subject)
            ? $"Invoice {invoice.Number} from your Chartered Accountants"
            : req.Subject!.Trim();
        var body = string.IsNullOrWhiteSpace(req.Body)
            ? $"Dear sir / madam,\r\n\r\nPlease find attached invoice {invoice.Number} dated " +
              $"{invoice.IssueDate:dd MMM yyyy} for ₹{invoice.TotalInr:N2}, due {invoice.DueDate:dd MMM yyyy}.\r\n\r\n" +
              "Thank you,\r\nAccounts team"
            : req.Body!;

        var mailOpts = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
        var outbox = new EmailOutboxEntry(
            toAddresses: string.Join(", ", toList),
            ccAddresses: ccList.Count == 0 ? null : string.Join(", ", ccList),
            subject: subject,
            bodyPlainText: body,
            fromAddress: mailOpts.FromAddress,
            fromName: mailOpts.FromName,
            attachmentCount: 1,
            attachmentTotalBytes: pdfBytes.LongLength,
            invoiceId: invoice.Id,
            senderUserId: _user.UserId,
            senderUserName: _user.UserName);
        _db.EmailOutbox.Add(outbox);
        await _db.SaveChangesAsync(ct);

        var msg = new EmailMessage(toList, ccList, subject, body,
            new List<EmailAttachment> { new(pdfFileName, "application/pdf", pdfBytes) });
        try
        {
            await _emailSender.SendAsync(msg, ct);
            outbox.MarkSent();
        }
        catch (Exception ex)
        {
            outbox.MarkFailed(ex.Message);
            await _db.SaveChangesAsync(ct);
            return StatusCode(StatusCodes.Status502BadGateway,
                new SendEmailResponse(outbox.Id, outbox.Status, outbox.ErrorMessage));
        }
        await _db.SaveChangesAsync(ct);
        return new SendEmailResponse(outbox.Id, outbox.Status, outbox.ErrorMessage);
    }

    /// <summary>Build the PDF bytes for a given invoice id (used by send-email). Null if missing.</summary>
    private async Task<(byte[] Bytes, string FileName)?> BuildPdfBytesAsync(Guid id, CancellationToken ct)
    {
        var model = await BuildPdfModelAsync(id, ct);
        if (model is null) return null;
        var fileName = $"{model.Number.Replace('/', '-')}.pdf";
        return (_pdf.Render(model), fileName);
    }

    // ----- helpers -----

    private async Task<string?> ValidateLineReferencesAsync(LineItemRequest li, CancellationToken ct)
    {
        if (li.ServiceDefinitionId is Guid sdid && sdid != Guid.Empty &&
            !await _db.ServiceDefinitions.AnyAsync(s => s.Id == sdid, ct))
            return "unknown_service_on_line";
        if (li.ServiceFileId is Guid sfid && sfid != Guid.Empty &&
            !await _db.ServiceFiles.AnyAsync(f => f.Id == sfid, ct))
            return "unknown_service_file_on_line";
        if (li.ComplianceObligationId is Guid oid && oid != Guid.Empty &&
            !await _db.ComplianceObligations.AnyAsync(o => o.Id == oid, ct))
            return "unknown_obligation_on_line";
        return null;
    }

    private async Task<List<InvoiceDto>> LoadDtosAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return new();

        var rows = await (from i in _db.Invoices.AsNoTracking()
                          where ids.Contains(i.Id)
                          join c in _db.Clients.AsNoTracking() on i.ClientId equals c.Id
                          join b in _db.Branches.AsNoTracking() on i.BranchId equals b.Id
                          join ps in _db.States.AsNoTracking() on i.PlaceOfSupplyStateId equals ps.Id
                          select new { i, c, b, ps }).ToListAsync(ct);

        var lines = await (from l in _db.InvoiceLineItems.AsNoTracking()
                           where ids.Contains(l.InvoiceId)
                           join s in _db.ServiceDefinitions.AsNoTracking() on l.ServiceDefinitionId equals s.Id into sj
                           from s in sj.DefaultIfEmpty()
                           join sf in _db.ServiceFiles.AsNoTracking() on l.ServiceFileId equals sf.Id into sfj
                           from sf in sfj.DefaultIfEmpty()
                           join ob in _db.ComplianceObligations.AsNoTracking() on l.ComplianceObligationId equals ob.Id into obj
                           from ob in obj.DefaultIfEmpty()
                           select new { l, s, sf, ob }).ToListAsync(ct);
        var linesByInvoice = lines
            .GroupBy(x => x.l.InvoiceId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.l.LineNumber)
                .Select(x => new LineItemDto(
                    x.l.Id, x.l.LineNumber, x.l.Description,
                    x.s == null ? null : x.s.Id, x.s == null ? null : x.s.Code,
                    x.sf == null ? null : x.sf.Id, x.sf == null ? null : x.sf.FileNumber,
                    x.ob == null ? null : x.ob.Id, x.ob == null ? null : x.ob.PeriodKey,
                    x.l.Quantity, x.l.UnitPriceInr, x.l.GstRatePercent,
                    x.l.LineSubtotalInr, x.l.LineGstInr, x.l.LineTotalInr))
                .ToList());

        return rows.Select(r => new InvoiceDto(
            r.i.Id, r.i.Number, r.i.FinancialYear,
            r.c.Id, r.c.Code, r.c.LegalName,
            r.b.Id, r.b.Code, r.b.Name,
            r.ps.Id, r.ps.Name,
            r.i.Status, r.i.IssueDate, r.i.DueDate, r.i.Notes,
            r.i.IsIntraState,
            r.i.SubtotalInr, r.i.CgstInr, r.i.SgstInr, r.i.IgstInr, r.i.TotalInr,
            r.i.PaidInr, r.i.CreditedInr,
            Math.Round(r.i.TotalInr - r.i.PaidInr - r.i.CreditedInr, 2),
            (IReadOnlyList<LineItemDto>)(linesByInvoice.GetValueOrDefault(r.i.Id, new List<LineItemDto>())),
            r.i.Version,
            r.i.EInvoiceStatus, r.i.Irn, r.i.AckNo, r.i.AckDateUtc)).ToList();
    }
}
