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
[Route("api/v{version:apiVersion}/masters/states")]
public class StatesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public StatesController(CmsDbContext db) => _db = db;

    private static StateDto Map(State e) => new(e.Id, e.Code, e.Name, e.CountryCode, e.DisplayOrder, e.IsActive);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<IEnumerable<StateDto>>> List([FromQuery] MasterListQuery q, [FromQuery] string? country, CancellationToken ct)
    {
        var query = _db.States.AsQueryable();
        if (!string.IsNullOrWhiteSpace(country)) query = query.Where(x => x.CountryCode == country.ToUpper());
        if (q.Active is not null) query = query.Where(x => x.IsActive == q.Active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
        }
        var rows = await query.OrderBy(x => x.CountryCode).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<StateDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.States.FindAsync(new object?[] { id }, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<StateDto>> Create([FromBody] StateUpsertDto dto, CancellationToken ct)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var cc = dto.CountryCode.Trim().ToUpperInvariant();
        if (await _db.States.AnyAsync(x => x.Code == code && x.CountryCode == cc, ct))
            return Conflict(new { error = "duplicate_code", code, countryCode = cc });

        var row = new State(dto.Code, dto.Name, dto.CountryCode, dto.DisplayOrder);
        _db.States.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<StateDto>> Update(Guid id, [FromBody] StateUpsertDto dto, CancellationToken ct)
    {
        var row = await _db.States.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();

        var newCode = dto.Code.Trim().ToUpperInvariant();
        var newCountry = dto.CountryCode.Trim().ToUpperInvariant();
        if ((newCode != row.Code || newCountry != row.CountryCode) &&
            await _db.States.AnyAsync(x => x.Code == newCode && x.CountryCode == newCountry && x.Id != id, ct))
            return Conflict(new { error = "duplicate_code", code = newCode, countryCode = newCountry });

        row.Update(dto.Code, dto.Name, dto.CountryCode, dto.DisplayOrder);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var row = await _db.States.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();
        row.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
