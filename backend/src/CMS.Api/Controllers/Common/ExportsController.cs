using Asp.Versioning;
using ClosedXML.Excel;
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
[Route("api/v{version:apiVersion}/exports")]
public class ExportsController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly CmsDbContext _db;
    private readonly IAuditService _audit;

    public ExportsController(CmsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private async Task<FileContentResult> WorkbookAsync(string sheetName, string fileName,
                                                       Action<IXLWorksheet> write)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);
        write(ws);
        try { ws.Columns().AdjustToContents(); } catch { /* best-effort */ }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return await Task.FromResult(File(ms.ToArray(), XlsxContentType, fileName));
    }

    private static void WriteHeader(IXLWorksheet ws, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
    }

    // -------- Clients --------

    [HttpGet("clients.xlsx")]
    [HasPermission(Permissions.Clients.Read)]
    [HasPermission(Permissions.Exports.Perform)]
    public async Task<ActionResult> ExportClients(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        [FromQuery] Guid? entityTypeId,
        [FromQuery] Guid? clientGroupId,
        CancellationToken ct = default)
    {
        var q = _db.Clients.AsNoTracking().Include(c => c.EntityType).Include(c => c.ClientGroup).AsQueryable();
        if (active is not null) q = q.Where(c => c.IsActive == active);
        if (entityTypeId is not null) q = q.Where(c => c.EntityTypeId == entityTypeId);
        if (clientGroupId is not null) q = q.Where(c => c.ClientGroupId == clientGroupId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(c =>
                c.Code.Contains(s) ||
                c.LegalName.ToLower().Contains(sLower) ||
                (c.TradeName != null && c.TradeName.ToLower().Contains(sLower)) ||
                (c.Pan != null && c.Pan.Contains(s.ToUpper())) ||
                (c.LegacyClientId != null && c.LegacyClientId.ToLower().Contains(sLower)));
        }

        var rows = await q.OrderBy(c => c.Code).ToListAsync(ct);

        var fileName = $"clients-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        var result = await WorkbookAsync("Clients", fileName, ws =>
        {
            WriteHeader(ws, "Code", "Legal Name", "Trade Name", "PAN", "Legacy Id",
                            "Entity Type", "Client Group", "Active", "Version");
            int r = 2;
            foreach (var c in rows)
            {
                ws.Cell(r, 1).Value = c.Code;
                ws.Cell(r, 2).Value = c.LegalName;
                ws.Cell(r, 3).Value = c.TradeName ?? "";
                ws.Cell(r, 4).Value = c.Pan ?? "";
                ws.Cell(r, 5).Value = c.LegacyClientId ?? "";
                ws.Cell(r, 6).Value = c.EntityType?.Name ?? "";
                ws.Cell(r, 7).Value = c.ClientGroup?.Name ?? "";
                ws.Cell(r, 8).Value = c.IsActive ? "Yes" : "No";
                ws.Cell(r, 9).Value = c.Version;
                r++;
            }
        });

        await _audit.RecordExportAsync(
            entityType: "Client",
            context: "Export:Clients",
            rowCount: rows.Count,
            filter: new { search, active, entityTypeId, clientGroupId },
            ct: ct);

        return result;
    }

    // -------- Service files --------

    [HttpGet("service-files.xlsx")]
    [HasPermission(Permissions.Clients.Read)]
    [HasPermission(Permissions.Exports.Perform)]
    public async Task<ActionResult> ExportServiceFiles(
        [FromQuery] string? search,
        [FromQuery] ServiceFileStatus? status,
        [FromQuery] Guid? serviceDefinitionId,
        [FromQuery] Guid? clientId,
        CancellationToken ct = default)
    {
        var q = _db.ServiceFiles.AsNoTracking()
            .Include(f => f.Client).ThenInclude(c => c!.ClientGroup)
            .Include(f => f.ServiceDefinition).Include(f => f.Branch).AsQueryable();
        if (clientId is not null) q = q.Where(f => f.ClientId == clientId);
        if (status is not null) q = q.Where(f => f.Status == status);
        if (serviceDefinitionId is not null) q = q.Where(f => f.ServiceDefinitionId == serviceDefinitionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(f =>
                f.FileNumber.Contains(s) ||
                (f.LegacyFileNumber != null && f.LegacyFileNumber.ToLower().Contains(sLower)) ||
                (f.Client != null && (f.Client.Code.Contains(s) || f.Client.LegalName.ToLower().Contains(sLower))));
        }

        var rows = await q.OrderByDescending(f => f.OpenedOn).ToListAsync(ct);
        // Resolve GSTIN/TAN for the involved branches in one shot.
        var branchIds = rows.Where(r => r.BranchId.HasValue).Select(r => r.BranchId!.Value).Distinct().ToArray();
        var regs = await _db.TaxRegistrations.AsNoTracking()
            .Where(r => branchIds.Contains(r.BranchId) && r.IsActive)
            .Select(r => new { r.BranchId, r.Type, r.Number })
            .ToListAsync(ct);
        string? gst(Guid? b) => b is null ? null : regs.FirstOrDefault(x => x.BranchId == b && x.Type == TaxRegistrationType.Gstin)?.Number;
        string? tan(Guid? b) => b is null ? null : regs.FirstOrDefault(x => x.BranchId == b && x.Type == TaxRegistrationType.Tan)?.Number;

        var fileName = $"service-files-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        var result = await WorkbookAsync("ServiceFiles", fileName, ws =>
        {
            WriteHeader(ws, "File Number", "Legacy File No", "Client Code", "Client Legal Name",
                            "PAN", "GSTIN", "TAN", "Group",
                            "Service", "Branch", "Status", "Opened On", "Closed On", "Metadata JSON", "Notes");
            int r = 2;
            foreach (var f in rows)
            {
                ws.Cell(r, 1).Value = f.FileNumber;
                ws.Cell(r, 2).Value = f.LegacyFileNumber ?? "";
                ws.Cell(r, 3).Value = f.Client?.Code ?? "";
                ws.Cell(r, 4).Value = f.Client?.LegalName ?? "";
                ws.Cell(r, 5).Value = f.Client?.Pan ?? "";
                ws.Cell(r, 6).Value = gst(f.BranchId) ?? "";
                ws.Cell(r, 7).Value = tan(f.BranchId) ?? "";
                ws.Cell(r, 8).Value = f.Client?.ClientGroup?.Name ?? "";
                ws.Cell(r, 9).Value = f.ServiceDefinition?.Name ?? "";
                ws.Cell(r, 10).Value = f.Branch?.Code ?? "(client-level)";
                ws.Cell(r, 11).Value = f.Status.ToString();
                ws.Cell(r, 12).Value = f.OpenedOn.ToString("yyyy-MM-dd");
                ws.Cell(r, 13).Value = f.ClosedOn?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(r, 14).Value = f.MetadataJson ?? "";
                ws.Cell(r, 15).Value = f.Notes ?? "";
                r++;
            }
        });

        await _audit.RecordExportAsync(
            entityType: "ServiceFile",
            context: "Export:ServiceFiles",
            rowCount: rows.Count,
            filter: new { search, status, serviceDefinitionId, clientId },
            ct: ct);

        return result;
    }

    // -------- Branches --------

    [HttpGet("branches.xlsx")]
    [HasPermission(Permissions.Clients.Read)]
    [HasPermission(Permissions.Exports.Perform)]
    public async Task<ActionResult> ExportBranches(
        [FromQuery] Guid? clientId, [FromQuery] bool? active, CancellationToken ct = default)
    {
        var q = _db.Branches.AsNoTracking().Include(b => b.State).AsQueryable();
        if (clientId is not null) q = q.Where(b => b.ClientId == clientId);
        if (active is not null) q = q.Where(b => b.IsActive == active);
        var branches = await q.ToListAsync(ct);

        var clientIds = branches.Select(b => b.ClientId).Distinct().ToArray();
        var clients = await _db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.LegalName })
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = branches
            .Select(b => new {
                Branch = b,
                ClientCode = clients.GetValueOrDefault(b.ClientId)?.Code ?? "",
                ClientLegal = clients.GetValueOrDefault(b.ClientId)?.LegalName ?? ""
            })
            .OrderBy(x => x.ClientCode).ThenByDescending(x => x.Branch.IsPrimary).ThenBy(x => x.Branch.Code)
            .ToList();

        var fileName = $"branches-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        var result = await WorkbookAsync("Branches", fileName, ws =>
        {
            WriteHeader(ws, "Client Code", "Client", "Code", "Name", "Type", "State",
                            "Primary", "Billing Enabled", "Active", "Opened On", "Closed On",
                            "Address Line1", "Address Line2", "City", "Pincode");
            int r = 2;
            foreach (var x in rows)
            {
                var b = x.Branch;
                ws.Cell(r, 1).Value = x.ClientCode;
                ws.Cell(r, 2).Value = x.ClientLegal;
                ws.Cell(r, 3).Value = b.Code;
                ws.Cell(r, 4).Value = b.Name;
                ws.Cell(r, 5).Value = b.Type.ToString();
                ws.Cell(r, 6).Value = b.State?.Name ?? "";
                ws.Cell(r, 7).Value = b.IsPrimary ? "Yes" : "No";
                ws.Cell(r, 8).Value = b.IsBillingEnabled ? "Yes" : "No";
                ws.Cell(r, 9).Value = b.IsActive ? "Yes" : "No";
                ws.Cell(r, 10).Value = b.OpenedOn.ToString("yyyy-MM-dd");
                ws.Cell(r, 11).Value = b.ClosedOn?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(r, 12).Value = b.Address.Line1;
                ws.Cell(r, 13).Value = b.Address.Line2 ?? "";
                ws.Cell(r, 14).Value = b.Address.City;
                ws.Cell(r, 15).Value = b.Address.Pincode;
                r++;
            }
        });

        await _audit.RecordExportAsync(
            entityType: "Branch",
            context: "Export:Branches",
            rowCount: rows.Count,
            filter: new { clientId, active },
            ct: ct);

        return result;
    }
}
