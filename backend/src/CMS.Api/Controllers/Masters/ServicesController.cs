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
[Route("api/v{version:apiVersion}/masters/services")]
public class ServicesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ServicesController(CmsDbContext db) => _db = db;

    private static ServiceDto Map(ServiceDefinition e) =>
        new(e.Id, e.Code, e.Name, e.Category, e.Description, e.DisplayOrder, e.IsActive,
            e.RequiredFieldsJson, e.OptionalFieldsJson, e.Frequency, e.DueDayOffsetDays);

    [HttpGet]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> List([FromQuery] MasterListQuery q, [FromQuery] string? category, CancellationToken ct)
    {
        var query = _db.ServiceDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
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
    public async Task<ActionResult<ServiceDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.ServiceDefinitions.FindAsync(new object?[] { id }, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<ServiceDto>> Create([FromBody] ServiceUpsertDto dto, CancellationToken ct)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.ServiceDefinitions.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { error = "duplicate_code", code });

        var row = new ServiceDefinition(dto.Code, dto.Name, dto.Category, dto.Description, dto.DisplayOrder);
        row.SetSchema(dto.RequiredFieldsJson, dto.OptionalFieldsJson);
        row.SetCompliance(dto.Frequency, dto.DueDayOffsetDays);
        _db.ServiceDefinitions.Add(row);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = row.Id, version = "1.0" }, Map(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<ActionResult<ServiceDto>> Update(Guid id, [FromBody] ServiceUpsertDto dto, CancellationToken ct)
    {
        var row = await _db.ServiceDefinitions.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();

        var newCode = dto.Code.Trim().ToUpperInvariant();
        if (newCode != row.Code && await _db.ServiceDefinitions.AnyAsync(x => x.Code == newCode && x.Id != id, ct))
            return Conflict(new { error = "duplicate_code", code = newCode });

        row.Update(dto.Code, dto.Name, dto.Category, dto.Description, dto.DisplayOrder);
        row.SetSchema(dto.RequiredFieldsJson, dto.OptionalFieldsJson);
        row.SetCompliance(dto.Frequency, dto.DueDayOffsetDays);
        await _db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpGet("{id:guid}/schema")]
    [HasPermission(Permissions.Masters.Read)]
    public async Task<ActionResult<object>> GetSchema(Guid id, CancellationToken ct)
    {
        var row = await _db.ServiceDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return NotFound();
        return new
        {
            serviceId = row.Id,
            code = row.Code,
            name = row.Name,
            required = CMS.Application.Clients.ServiceFieldSerializer.ReadFieldDefs(row.RequiredFieldsJson),
            optional = CMS.Application.Clients.ServiceFieldSerializer.ReadFieldDefs(row.OptionalFieldsJson),
        };
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Masters.Write)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var row = await _db.ServiceDefinitions.FindAsync(new object?[] { id }, ct);
        if (row is null) return NotFound();
        row.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
