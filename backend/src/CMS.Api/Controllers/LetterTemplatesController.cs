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
[Route("api/v{version:apiVersion}/letter-templates")]
public class LetterTemplatesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public LetterTemplatesController(CmsDbContext db) => _db = db;

    public record LetterTemplateDto(Guid Id, string Name, string Body, bool IsActive,
        DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, int Version);

    public record LetterTemplateUpsertRequest(
        [Required, StringLength(200)] string Name,
        [Required] string Body);

    [HttpGet]
    [HasPermission(Permissions.Letters.Read)]
    public async Task<ActionResult<IReadOnlyList<LetterTemplateDto>>> List(
        [FromQuery] bool? activeOnly = true, CancellationToken ct = default)
    {
        var q = _db.LetterTemplates.AsNoTracking().AsQueryable();
        if (activeOnly == true) q = q.Where(t => t.IsActive);
        var rows = await q.Select(t => new LetterTemplateDto(t.Id, t.Name, t.Body, t.IsActive,
            t.CreatedAtUtc, t.UpdatedAtUtc, t.Version)).ToListAsync(ct);
        return rows.OrderBy(r => r.Name).ToList();
    }

    [HttpPost]
    [HasPermission(Permissions.Letters.Write)]
    public async Task<ActionResult<LetterTemplateDto>> Create([FromBody] LetterTemplateUpsertRequest req, CancellationToken ct)
    {
        if (await _db.LetterTemplates.AnyAsync(t => t.Name == req.Name, ct))
            return Conflict(new { error = "duplicate_name" });
        var row = new LetterTemplate(req.Name, req.Body);
        _db.LetterTemplates.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { version = "1.0" },
            new LetterTemplateDto(row.Id, row.Name, row.Body, row.IsActive, row.CreatedAtUtc, row.UpdatedAtUtc, row.Version));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Letters.Write)]
    public async Task<ActionResult<LetterTemplateDto>> Update(Guid id, [FromBody] LetterTemplateUpsertRequest req, CancellationToken ct)
    {
        var row = await _db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        if (row.Name != req.Name && await _db.LetterTemplates.AnyAsync(t => t.Name == req.Name && t.Id != id, ct))
            return Conflict(new { error = "duplicate_name" });
        row.Update(req.Name, req.Body);
        await _db.SaveChangesAsync(ct);
        return new LetterTemplateDto(row.Id, row.Name, row.Body, row.IsActive, row.CreatedAtUtc, row.UpdatedAtUtc, row.Version);
    }

    [HttpPost("{id:guid}/active")]
    [HasPermission(Permissions.Letters.Write)]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        var row = await _db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        row.SetActive(value);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Letters.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await _db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        _db.LetterTemplates.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
