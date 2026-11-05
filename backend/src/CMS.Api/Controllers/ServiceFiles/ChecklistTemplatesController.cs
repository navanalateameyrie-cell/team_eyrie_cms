using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masters/services/{serviceId:guid}/checklist-template")]
public class ChecklistTemplatesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ChecklistTemplatesController(CmsDbContext db) => _db = db;

    public record TemplateItemDto(Guid Id, int Order, string Text, bool IsRequired);
    public record TemplateDto(Guid Id, Guid ServiceDefinitionId, string Name, IReadOnlyList<TemplateItemDto> Items);

    public record TemplateItemInput(
        [Range(0, 10_000)] int Order,
        [Required, StringLength(500)] string Text,
        bool IsRequired);
    public record UpsertTemplateRequest(
        [Required, StringLength(200)] string Name,
        [Required] List<TemplateItemInput> Items);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<TemplateDto?>> Get(Guid serviceId, CancellationToken ct)
    {
        var t = await _db.ChecklistTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ServiceDefinitionId == serviceId, ct);
        if (t is null) return (TemplateDto?)null;
        var items = await _db.ChecklistTemplateItems.AsNoTracking()
            .Where(x => x.TemplateId == t.Id)
            .OrderBy(x => x.Order)
            .Select(x => new TemplateItemDto(x.Id, x.Order, x.Text, x.IsRequired))
            .ToListAsync(ct);
        return new TemplateDto(t.Id, t.ServiceDefinitionId, t.Name, items);
    }

    [HttpPut]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<TemplateDto>> Upsert(Guid serviceId, [FromBody] UpsertTemplateRequest req, CancellationToken ct)
    {
        if (!await _db.ServiceDefinitions.AnyAsync(s => s.Id == serviceId, ct))
            return UnprocessableEntity(new { error = "unknown_service" });

        var template = await _db.ChecklistTemplates.FirstOrDefaultAsync(x => x.ServiceDefinitionId == serviceId, ct);
        if (template is null)
        {
            template = new ChecklistTemplate(serviceId, req.Name);
            _db.ChecklistTemplates.Add(template);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            template.Rename(req.Name);
        }

        // Replace-all strategy: delete existing items and re-create. Existing engagement checklists
        // keep their frozen copies — editing a template does not retroactively touch instances.
        var oldItems = await _db.ChecklistTemplateItems.Where(i => i.TemplateId == template.Id).ToListAsync(ct);
        _db.ChecklistTemplateItems.RemoveRange(oldItems);
        var newRows = req.Items.Select(i => new ChecklistTemplateItem(template.Id, i.Order, i.Text, i.IsRequired)).ToList();
        _db.ChecklistTemplateItems.AddRange(newRows);
        await _db.SaveChangesAsync(ct);

        return new TemplateDto(template.Id, template.ServiceDefinitionId, template.Name,
            newRows.OrderBy(x => x.Order).Select(x => new TemplateItemDto(x.Id, x.Order, x.Text, x.IsRequired)).ToList());
    }

    [HttpDelete]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Delete(Guid serviceId, CancellationToken ct)
    {
        var template = await _db.ChecklistTemplates.FirstOrDefaultAsync(x => x.ServiceDefinitionId == serviceId, ct);
        if (template is null) return NotFound();
        _db.ChecklistTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
