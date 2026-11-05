using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Application.Letters;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance/{obligationId:guid}/letter")]
public class ObligationLetterController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ILetterRenderer _renderer;
    private readonly ISettingsStore _settings;
    public ObligationLetterController(CmsDbContext db, ILetterRenderer renderer, ISettingsStore settings)
    { _db = db; _renderer = renderer; _settings = settings; }

    public record LetterPreviewDto(string Title, string Body, IReadOnlyDictionary<string, string> Context);

    [HttpGet("preview")]
    [HasPermission(Permissions.Letters.Read)]
    public async Task<ActionResult<LetterPreviewDto>> Preview(Guid obligationId,
        [FromQuery] Guid templateId, CancellationToken ct)
    {
        var (template, context, title) = await LoadAsync(obligationId, templateId, ct);
        if (template is null) return NotFound();
        var merged = _renderer.RenderText(template.Body, context);
        return new LetterPreviewDto(title, merged, context.Tokens);
    }

    [HttpGet]
    [HasPermission(Permissions.Letters.Read)]
    public async Task<IActionResult> Pdf(Guid obligationId, [FromQuery] Guid templateId, CancellationToken ct)
    {
        var (template, context, title) = await LoadAsync(obligationId, templateId, ct);
        if (template is null) return NotFound();
        var bytes = _renderer.RenderPdf(title, template.Body, context);
        return File(bytes, "application/pdf", $"letter-{template.Name}-{obligationId:N}.pdf");
    }

    private async Task<(LetterTemplate? Template, LetterContext Context, string Title)> LoadAsync(
        Guid obligationId, Guid templateId, CancellationToken ct)
    {
        var template = await _db.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return (null, new LetterContext(new Dictionary<string, string>()), "");

        var row = await (from o in _db.ComplianceObligations.AsNoTracking()
                         where o.Id == obligationId
                         join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                         join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                         join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                         select new { o, f, c, s }).FirstOrDefaultAsync(ct);
        if (row is null) return (null, new LetterContext(new Dictionary<string, string>()), "");

        string? gstin = null;
        if (row.f.BranchId is Guid bid)
            gstin = await _db.TaxRegistrations.AsNoTracking()
                .Where(r => r.BranchId == bid && r.Type == TaxRegistrationType.Gstin && r.IsActive)
                .Select(r => r.Number).FirstOrDefaultAsync(ct);

        var firm = await _settings.GetAsync<FirmProfileOptions>(SettingKeys.FirmProfile, ct);

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client.name"]      = row.c.LegalName,
            ["client.tradeName"] = row.c.TradeName ?? "",
            ["client.pan"]       = row.c.Pan ?? "",
            ["client.gstin"]     = gstin ?? "",
            ["client.code"]      = row.c.Code,
            ["service.code"]     = row.s.Code,
            ["service.name"]     = row.s.Name,
            ["file.number"]      = row.f.FileNumber,
            ["obligation.periodKey"] = row.o.PeriodKey,
            ["obligation.dueDate"]   = row.o.DueDate.ToString("dd MMM yyyy"),
            ["obligation.status"]    = row.o.Status.ToString(),
            ["firm.name"]        = firm.Name ?? "",
            ["firm.gstin"]       = firm.Gstin ?? "",
            ["firm.pan"]         = firm.Pan ?? "",
            ["today"]            = DateTime.UtcNow.ToString("dd MMM yyyy"),
        };
        var title = $"{template.Name} — {row.c.LegalName} / {row.s.Code} / {row.o.PeriodKey}";
        return (template, new LetterContext(tokens), title);
    }
}
