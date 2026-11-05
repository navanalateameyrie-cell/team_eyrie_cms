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
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/branches")]
public class ClientBranchesController : ControllerBase
{
    private readonly CmsDbContext _db;

    public ClientBranchesController(CmsDbContext db) => _db = db;

    public record AddressDto(
        [Required, StringLength(200)] string Line1,
        [StringLength(200)] string? Line2,
        [Required, StringLength(100)] string City,
        [Required, StringLength(16)] string Pincode);

    public record BranchDto(Guid Id, Guid ClientId, string Code, string Name, BranchType Type,
                            Guid StateId, string? StateName,
                            AddressDto Address, bool IsPrimary, bool IsBillingEnabled, bool IsActive,
                            DateOnly OpenedOn, DateOnly? ClosedOn, int Version);

    public record CreateBranchRequest(
        [Required, StringLength(32)] string Code,
        [Required, StringLength(200)] string Name,
        [Required] BranchType Type,
        [Required] Guid StateId,
        [Required] AddressDto Address,
        [Required] DateOnly OpenedOn,
        bool IsPrimary = false,
        bool IsBillingEnabled = true);

    public record UpdateBranchRequest(
        [Required, StringLength(32)] string Code,
        [Required, StringLength(200)] string Name,
        [Required] BranchType Type,
        [Required] Guid StateId,
        [Required] AddressDto Address,
        bool IsBillingEnabled,
        [Required] int Version);

    private static BranchDto Map(Branch b) => new(
        b.Id, b.ClientId, b.Code, b.Name, b.Type, b.StateId, b.State?.Name,
        new AddressDto(b.Address.Line1, b.Address.Line2, b.Address.City, b.Address.Pincode),
        b.IsPrimary, b.IsBillingEnabled, b.IsActive, b.OpenedOn, b.ClosedOn, b.Version);

    private async Task<bool> ClientExistsAsync(Guid clientId, CancellationToken ct) =>
        await _db.Clients.AnyAsync(c => c.Id == clientId, ct);

    private async Task<bool> StateExistsAsync(Guid stateId, CancellationToken ct) =>
        await _db.States.AnyAsync(s => s.Id == stateId && s.IsActive, ct);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List(Guid clientId, [FromQuery] bool? active, CancellationToken ct)
    {
        if (!await ClientExistsAsync(clientId, ct)) return NotFound();
        var q = _db.Branches.AsNoTracking().Include(b => b.State).Where(b => b.ClientId == clientId);
        if (active is not null) q = q.Where(b => b.IsActive == active);
        var rows = await q.OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Code).ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<BranchDto>> Get(Guid clientId, Guid id, CancellationToken ct)
    {
        var row = await _db.Branches.AsNoTracking().Include(b => b.State)
            .FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == id, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<BranchDto>> Create(Guid clientId, [FromBody] CreateBranchRequest req, CancellationToken ct)
    {
        if (!await ClientExistsAsync(clientId, ct)) return NotFound();
        if (!await StateExistsAsync(req.StateId, ct)) return BadRequest(new { error = "invalid_state" });

        if (await _db.Branches.AnyAsync(b => b.ClientId == clientId && b.Code == req.Code.ToUpperInvariant(), ct))
            return Conflict(new { error = "duplicate_branch_code" });

        var address = new Address(req.Address.Line1, req.Address.City, req.Address.Pincode, req.Address.Line2);

        // Auto-primary rules: first branch on a client is always primary; if caller requests primary
        // and another primary exists, demote the old one.
        var hasAnyBranch = await _db.Branches.AnyAsync(b => b.ClientId == clientId, ct);
        var isPrimary = req.IsPrimary || !hasAnyBranch;

        var branch = new Branch(clientId, req.Code, req.Name, req.Type, req.StateId,
                                address, req.OpenedOn, isPrimary, req.IsBillingEnabled);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (isPrimary)
        {
            var existingPrimary = await _db.Branches
                .Where(b => b.ClientId == clientId && b.IsPrimary)
                .ToListAsync(ct);
            foreach (var p in existingPrimary) p.DemotePrimary();
        }
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _db.Entry(branch).Reference(b => b.State).LoadAsync(ct);
        return CreatedAtAction(nameof(Get), new { clientId, id = branch.Id, version = "1.0" }, Map(branch));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<BranchDto>> Update(Guid clientId, Guid id, [FromBody] UpdateBranchRequest req, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == id, ct);
        if (branch is null) return NotFound();
        if (!await StateExistsAsync(req.StateId, ct)) return BadRequest(new { error = "invalid_state" });

        var newCode = req.Code.Trim().ToUpperInvariant();
        if (newCode != branch.Code &&
            await _db.Branches.AnyAsync(b => b.ClientId == clientId && b.Code == newCode && b.Id != id, ct))
            return Conflict(new { error = "duplicate_branch_code" });

        try
        {
            _db.Entry(branch).Property(b => b.Version).OriginalValue = req.Version;
            var address = new Address(req.Address.Line1, req.Address.City, req.Address.Pincode, req.Address.Line2);
            branch.UpdateProfile(req.Code, req.Name, req.Type, req.StateId, address, req.IsBillingEnabled);
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "invalid_branch_state", detail = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "concurrency_conflict" });
        }

        await _db.Entry(branch).Reference(b => b.State).LoadAsync(ct);
        return Map(branch);
    }

    [HttpPost("{id:guid}/make-primary")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<BranchDto>> MakePrimary(Guid clientId, Guid id, [FromQuery, Required] int version, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == id, ct);
        if (branch is null) return NotFound();
        if (!branch.IsActive) return BadRequest(new { error = "primary_must_be_active" });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var othersPrimary = await _db.Branches
            .Where(b => b.ClientId == clientId && b.IsPrimary && b.Id != id)
            .ToListAsync(ct);
        foreach (var p in othersPrimary) p.DemotePrimary();

        try
        {
            _db.Entry(branch).Property(b => b.Version).OriginalValue = version;
            branch.MarkPrimary();
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "concurrency_conflict" });
        }

        await _db.Entry(branch).Reference(b => b.State).LoadAsync(ct);
        return Map(branch);
    }

    [HttpPost("{id:guid}/close")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<BranchDto>> Close(Guid clientId, Guid id,
        [FromQuery, Required] int version, [FromQuery] DateOnly? closedOn, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == id, ct);
        if (branch is null) return NotFound();
        if (!branch.IsActive) return Ok(Map(branch));

        if (branch.IsPrimary)
        {
            var otherActive = await _db.Branches.AnyAsync(
                b => b.ClientId == clientId && b.Id != id && b.IsActive, ct);
            if (otherActive)
                return Conflict(new { error = "primary_branch_requires_replacement",
                                      hint = "Make another active branch primary before closing this one." });
            // Otherwise it's the only active branch — closing it leaves client with no primary;
            // we do NOT touch the client.
        }

        try
        {
            _db.Entry(branch).Property(b => b.Version).OriginalValue = version;
            branch.Close(closedOn ?? DateOnly.FromDateTime(DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }

        await _db.Entry(branch).Reference(b => b.State).LoadAsync(ct);
        return Map(branch);
    }

    [HttpPost("{id:guid}/reopen")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<BranchDto>> Reopen(Guid clientId, Guid id, [FromQuery, Required] int version, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == id, ct);
        if (branch is null) return NotFound();
        try
        {
            _db.Entry(branch).Property(b => b.Version).OriginalValue = version;
            branch.Reopen();
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }

        await _db.Entry(branch).Reference(b => b.State).LoadAsync(ct);
        return Map(branch);
    }
}
