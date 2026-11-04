using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers.Masters;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masters/entity-types")]
public class EntityTypesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public EntityTypesController(CmsDbContext db) => _db = db;

    private static EntityTypeDto Map(EntityType e) => new(e.Id, e.Code, e.Name, e.Description, e.DisplayOrder, e.IsActive);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<IEnumerable<EntityTypeDto>>> List([FromQuery] MasterListQuery q, CancellationToken ct)
    {
        var query = _db.EntityTypes.AsQueryable();
        if (q.Active is not null) query = query.Where(x => x.IsActive == q.Active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
        }
        var rows = await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<EntityTypeDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.EntityTypes.FindAsync(new object?[] { id }, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<EntityTypeDto>> Create([FromBody] EntityTypeUpsertDto dto, CancellationToken ct)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.EntityTypes.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { error = "duplicate_code", code });

        var row = new EntityType(dto.Code, dto.Name, dto.Description, dto.DisplayOrder);
        _db.EntityTypes.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<EntityTypeDto>> Update(Guid id, [FromBody] EntityTypeUpsertDto dto, CancellationToken ct)
    {
        var row = await _db.EntityTypes.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();

        var newCode = dto.Code.Trim().ToUpperInvariant();
        if (newCode != row.Code && await _db.EntityTypes.AnyAsync(x => x.Code == newCode && x.Id != id, ct))
            return Conflict(new { error = "duplicate_code", code = newCode });

        row.Update(dto.Code, dto.Name, dto.Description, dto.DisplayOrder);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var row = await _db.EntityTypes.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();
        row.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
