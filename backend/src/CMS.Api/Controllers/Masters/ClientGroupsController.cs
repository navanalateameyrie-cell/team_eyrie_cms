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
[Route("api/v{version:apiVersion}/masters/client-groups")]
public class ClientGroupsController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ClientGroupsController(CmsDbContext db) => _db = db;

    private static ClientGroupDto Map(ClientGroup e) => new(e.Id, e.Code, e.Name, e.Description, e.IsActive);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<IEnumerable<ClientGroupDto>>> List([FromQuery] MasterListQuery q, CancellationToken ct)
    {
        var query = _db.ClientGroups.AsQueryable();
        if (q.Active is not null) query = query.Where(x => x.IsActive == q.Active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
        }
        var rows = await query.OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<ClientGroupDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.ClientGroups.FindAsync(new object?[] { id }, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<ClientGroupDto>> Create([FromBody] ClientGroupUpsertDto dto, CancellationToken ct)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.ClientGroups.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { error = "duplicate_code", code });

        var row = new ClientGroup(dto.Code, dto.Name, dto.Description);
        _db.ClientGroups.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<ClientGroupDto>> Update(Guid id, [FromBody] ClientGroupUpsertDto dto, CancellationToken ct)
    {
        var row = await _db.ClientGroups.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();

        var newCode = dto.Code.Trim().ToUpperInvariant();
        if (newCode != row.Code && await _db.ClientGroups.AnyAsync(x => x.Code == newCode && x.Id != id, ct))
            return Conflict(new { error = "duplicate_code", code = newCode });

        row.Update(dto.Code, dto.Name, dto.Description);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var row = await _db.ClientGroups.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();
        row.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
