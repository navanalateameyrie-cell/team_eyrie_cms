using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>
/// Branch-aware billing context: everything an invoice-generator or external accounting tool
/// needs about a client branch. This is NOT an accounting system — no bills, no numbering,
/// no pricing. It only exposes the identifying + addressing data and active file references.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients/{clientId:guid}")]
public class BillingContextController : ControllerBase
{
    private readonly CmsDbContext _db;
    public BillingContextController(CmsDbContext db) => _db = db;

    public record BillingAddressDto(string Line1, string? Line2, string City, string Pincode);

    public record ActiveFileRef(Guid Id, string FileNumber, string ServiceCode, string ServiceName, DateOnly OpenedOn);

    public record BillingContextDto(
        Guid ClientId, string ClientCode, string LegalName, string? TradeName, string? Pan,
        Guid BranchId, string BranchCode, string BranchName, BranchType BranchType,
        bool BranchIsPrimary, bool BranchIsBillingEnabled, bool BranchIsActive,
        string? StateCode, string? StateName, string CountryCode,
        BillingAddressDto Address, string? Gstin,
        IReadOnlyList<ActiveFileRef> ActiveServiceFiles);

    [HttpGet("branches/{branchId:guid}/billing-context")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<BillingContextDto>> ForBranch(Guid clientId, Guid branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().Include(b => b.State)
            .FirstOrDefaultAsync(b => b.ClientId == clientId && b.Id == branchId, ct);
        if (branch is null) return NotFound();
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return NotFound();

        if (!branch.IsBillingEnabled)
        {
            // 200 with a hint field so UIs can present the warning rather than refuse outright,
            // but the UI can also just show "billing disabled on this branch".
        }

        var gstin = await _db.TaxRegistrations.AsNoTracking()
            .Where(t => t.BranchId == branchId
                     && t.Type == TaxRegistrationType.Gstin
                     && t.IsActive)
            .Select(t => t.Number)
            .FirstOrDefaultAsync(ct);

        // Active service files attached to THIS branch, plus client-level (branchId == null) files
        // — both are billable through this branch's identifying details.
        var activeFiles = await _db.ServiceFiles.AsNoTracking()
            .Include(f => f.ServiceDefinition)
            .Where(f => f.ClientId == clientId
                     && f.Status != ServiceFileStatus.Closed
                     && (f.BranchId == branchId || f.BranchId == null))
            .OrderBy(f => f.ServiceDefinition!.Name)
            .ToListAsync(ct);

        var fileRefs = activeFiles.Select(f => new ActiveFileRef(
            f.Id, f.FileNumber,
            f.ServiceDefinition?.Code ?? "",
            f.ServiceDefinition?.Name ?? "",
            f.OpenedOn)).ToList();

        return new BillingContextDto(
            ClientId: client.Id,
            ClientCode: client.Code,
            LegalName: client.LegalName,
            TradeName: client.TradeName,
            Pan: client.Pan,
            BranchId: branch.Id,
            BranchCode: branch.Code,
            BranchName: branch.Name,
            BranchType: branch.Type,
            BranchIsPrimary: branch.IsPrimary,
            BranchIsBillingEnabled: branch.IsBillingEnabled,
            BranchIsActive: branch.IsActive,
            StateCode: branch.State?.Code,
            StateName: branch.State?.Name,
            CountryCode: branch.State?.CountryCode ?? "IN",
            Address: new BillingAddressDto(branch.Address.Line1, branch.Address.Line2,
                                            branch.Address.City, branch.Address.Pincode),
            Gstin: gstin,
            ActiveServiceFiles: fileRefs);
    }

    [HttpGet("billing-context")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<BillingContextDto>> ForPrimaryBranch(Guid clientId, CancellationToken ct)
    {
        var primary = await _db.Branches.AsNoTracking()
            .Where(b => b.ClientId == clientId && b.IsPrimary && b.IsActive)
            .Select(b => b.Id).FirstOrDefaultAsync(ct);
        if (primary == Guid.Empty)
            return NotFound(new { error = "no_primary_branch" });
        return await ForBranch(clientId, primary, ct);
    }
}
