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
[Route("api/v{version:apiVersion}/masters/staff")]
public class StaffController : ControllerBase
{
    private readonly CmsDbContext _db;
    public StaffController(CmsDbContext db) => _db = db;

    private static StaffDto Map(Staff e) => new(e.Id, e.Code, e.FullName, e.Email, e.Phone, e.Role, e.IsActive);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<IEnumerable<StaffDto>>> List([FromQuery] MasterListQuery q, CancellationToken ct)
    {
        var query = _db.Staff.AsQueryable();
        if (q.Active is not null) query = query.Where(x => x.IsActive == q.Active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s) || x.FullName.ToLower().Contains(s) || x.Email.ToLower().Contains(s));
        }
        var rows = await query.OrderBy(x => x.FullName).ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<StaffDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.Staff.FindAsync(new object?[] { id }, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<StaffDto>> Create([FromBody] StaffUpsertDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Staff.AnyAsync(x => x.Email == email, ct))
            return Conflict(new { error = "duplicate_email", email });

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _db.Staff.AnyAsync(x => x.Code == code, ct))
                return Conflict(new { error = "duplicate_code", code });
        }

        var row = new Staff(dto.FullName, dto.Email, dto.Code, dto.Phone, dto.Role);
        _db.Staff.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<StaffDto>> Update(Guid id, [FromBody] StaffUpsertDto dto, CancellationToken ct)
    {
        var row = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();

        var newEmail = dto.Email.Trim().ToLowerInvariant();
        if (newEmail != row.Email && await _db.Staff.AnyAsync(x => x.Email == newEmail && x.Id != id, ct))
            return Conflict(new { error = "duplicate_email", email = newEmail });

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var newCode = dto.Code.Trim().ToUpperInvariant();
            if (newCode != row.Code && await _db.Staff.AnyAsync(x => x.Code == newCode && x.Id != id, ct))
                return Conflict(new { error = "duplicate_code", code = newCode });
        }

        row.UpdateProfile(dto.FullName, dto.Email, dto.Code, dto.Phone, dto.Role);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var row = await _db.Staff.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();
        row.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
