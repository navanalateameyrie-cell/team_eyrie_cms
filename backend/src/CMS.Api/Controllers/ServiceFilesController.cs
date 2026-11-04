using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>Cross-client search for support use ("find file 1000000042").</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/service-files")]
public class ServiceFilesController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ServiceFilesController(CmsDbContext db) => _db = db;

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<ClientServiceFilesController.ServiceFileDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] ServiceFileStatus? status,
        [FromQuery] Guid? serviceDefinitionId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var q = _db.ServiceFiles.AsNoTracking()
            .Include(f => f.Client).Include(f => f.ServiceDefinition).Include(f => f.Branch).AsQueryable();

        if (status is not null) q = q.Where(f => f.Status == status);
        if (serviceDefinitionId is not null) q = q.Where(f => f.ServiceDefinitionId == serviceDefinitionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(f => f.FileNumber.Contains(s)
                             || (f.LegacyFileNumber != null && f.LegacyFileNumber.ToLower().Contains(sLower))
                             || (f.Client != null && (f.Client.Code.Contains(s)
                                                      || f.Client.LegalName.ToLower().Contains(sLower))));
        }

        var rows = await q.OrderByDescending(f => f.OpenedOn).Skip(skip).Take(take).ToListAsync(ct);
        return Ok(rows.Select(f => new ClientServiceFilesController.ServiceFileDto(
            f.Id, f.FileNumber, f.LegacyFileNumber,
            f.ClientId, f.Client?.Code, f.Client?.LegalName, f.Client?.Pan,
            f.Client?.ClientGroupId, null, null,
            f.ServiceDefinitionId, f.ServiceDefinition?.Code, f.ServiceDefinition?.Name,
            f.BranchId, f.Branch?.Code, f.Branch?.Name,
            null, null,
            f.Status, f.OpenedOn, f.ClosedOn, f.Notes, f.MetadataJson, f.Version)).ToList());
    }
}
