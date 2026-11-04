using System.ComponentModel.DataAnnotations;
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
[Route("api/v{version:apiVersion}/engagements/{engagementId:guid}/checklist")]
public class EngagementChecklistController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _me;
    public EngagementChecklistController(CmsDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public record ChecklistItemDto(Guid Id, int Order, string Text, bool IsRequired,
        bool IsCompleted, DateTimeOffset? CompletedAtUtc, Guid? CompletedByStaffId, string? Note);

    public record ChecklistDto(Guid? Id, bool Instantiated,
        int TotalItems, int CompletedItems, IReadOnlyList<ChecklistItemDto> Items);

    public record ToggleRequest([StringLength(2000)] string? Note);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<ChecklistDto>> Get(Guid engagementId, CancellationToken ct)
    {
        var engagement = await _db.Engagements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == engagementId, ct);
        if (engagement is null) return NotFound();

        // Lazy-instantiate on first read: resolve the template via the engagement's service file.
        var checklist = await _db.EngagementChecklists.AsNoTracking().FirstOrDefaultAsync(x => x.EngagementId == engagementId, ct);
        if (checklist is null)
        {
            var file = await _db.ServiceFiles.AsNoTracking().FirstAsync(f => f.Id == engagement.ServiceFileId, ct);
            var template = await _db.ChecklistTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.ServiceDefinitionId == file.ServiceDefinitionId, ct);
            if (template is null)
                return new ChecklistDto(null, false, 0, 0, Array.Empty<ChecklistItemDto>());

            var items = await _db.ChecklistTemplateItems.AsNoTracking()
                .Where(i => i.TemplateId == template.Id).OrderBy(i => i.Order).ToListAsync(ct);

            var instance = new EngagementChecklist(engagementId, template.Id);
            _db.EngagementChecklists.Add(instance);
            var instanceItems = items.Select(i =>
                new EngagementChecklistItem(instance.Id, i.Id, i.Text, i.Order, i.IsRequired)).ToList();
            _db.EngagementChecklistItems.AddRange(instanceItems);
            await _db.SaveChangesAsync(ct);

            return BuildDto(instance.Id, instanceItems);
        }

        var rows = await _db.EngagementChecklistItems.AsNoTracking()
            .Where(i => i.ChecklistId == checklist.Id).OrderBy(i => i.Order).ToListAsync(ct);
        return BuildDto(checklist.Id, rows);
    }

    [HttpPost("items/{itemId:guid}/toggle")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<ChecklistDto>> Toggle(Guid engagementId, Guid itemId,
        [FromBody] ToggleRequest? req, CancellationToken ct)
    {
        var item = await _db.EngagementChecklistItems
            .Include(i => i.Checklist)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null || item.Checklist?.EngagementId != engagementId) return NotFound();

        item.Toggle(_me.StaffId ?? Guid.Empty, req?.Note);
        await _db.SaveChangesAsync(ct);

        var rows = await _db.EngagementChecklistItems.AsNoTracking()
            .Where(i => i.ChecklistId == item.ChecklistId).OrderBy(i => i.Order).ToListAsync(ct);
        return BuildDto(item.ChecklistId, rows);
    }

    private static ChecklistDto BuildDto(Guid? checklistId, IReadOnlyList<EngagementChecklistItem> items)
    {
        var dtos = items.Select(x => new ChecklistItemDto(x.Id, x.Order, x.Text, x.IsRequired,
            x.IsCompleted, x.CompletedAtUtc, x.CompletedByStaffId, x.Note)).ToList();
        return new ChecklistDto(checklistId, true, dtos.Count, dtos.Count(d => d.IsCompleted), dtos);
    }
}
