using System.Globalization;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Auditing;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit")]
public class AuditController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly CmsDbContext _db;
    private readonly IAuditService _audit;
    public AuditController(CmsDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public record AuditEntryDto(
        Guid Id, DateTimeOffset OccurredAtUtc,
        string EntityType, string EntityId, AuditAction Action,
        string? ChangedFields, string? OldValues, string? NewValues,
        Guid? UserId, string? UserName, string? CorrelationId,
        string? IpAddress, string? UserAgent, string? Context,
        IReadOnlyList<FieldDiff>? Diff = null);

    public record ListResponse(int Total, int Skip, int Take, IReadOnlyList<AuditEntryDto> Items);

    public record FieldDiff(string Field, string? OldValue, string? NewValue, bool Changed);

    public record AuditEntryDetail(AuditEntryDto Entry, IReadOnlyList<FieldDiff> Diff);

    public record FilterOptionsDto(
        IReadOnlyList<string> EntityTypes,
        IReadOnlyList<UserOptionDto> Users,
        IReadOnlyList<ActionOptionDto> Actions);

    public record UserOptionDto(Guid? UserId, string UserName);
    public record ActionOptionDto(int Id, string Name);

    [HttpGet]
    [HasPermission(Permissions.Audit.Read)]
    public async Task<ActionResult<ListResponse>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] string? userName,
        [FromQuery] string? correlationId,
        [FromQuery] AuditAction? action,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var q = _db.AuditEntries.AsNoTracking().AsQueryable();

        if (clientId is not null)
        {
            var cId = clientId.Value;
            var cIdStr = cId.ToString();
            var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cId, ct);
            var relatedEntityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cIdStr };

            if (client is not null)
            {
                relatedEntityIds.Add(client.Code);
                if (!string.IsNullOrWhiteSpace(client.LegacyClientId))
                {
                    relatedEntityIds.Add(client.LegacyClientId);
                }
            }

            var branchGuids = await _db.Branches.AsNoTracking()
                .Where(b => b.ClientId == cId)
                .Select(b => b.Id)
                .ToListAsync(ct);
            foreach (var b in branchGuids) relatedEntityIds.Add(b.ToString());

            var sfGuids = await _db.ServiceFiles.AsNoTracking()
                .Where(sf => sf.ClientId == cId)
                .Select(sf => sf.Id)
                .ToListAsync(ct);
            foreach (var sf in sfGuids) relatedEntityIds.Add(sf.ToString());

            var engGuids = await _db.Engagements.AsNoTracking()
                .Where(e => sfGuids.Contains(e.ServiceFileId))
                .Select(e => e.Id)
                .ToListAsync(ct);
            foreach (var e in engGuids) relatedEntityIds.Add(e.ToString());

            var taxGuids = await _db.TaxRegistrations.AsNoTracking()
                .Where(t => branchGuids.Contains(t.BranchId))
                .Select(t => t.Id)
                .ToListAsync(ct);
            foreach (var t in taxGuids) relatedEntityIds.Add(t.ToString());

            var saGuids = await _db.StaffAssignments.AsNoTracking()
                .Where(a => (a.Scope == Domain.Entities.AssignmentScope.Client && a.ScopeId == cId) ||
                            (a.Scope == Domain.Entities.AssignmentScope.ServiceFile && sfGuids.Contains(a.ScopeId)))
                .Select(a => a.Id)
                .ToListAsync(ct);
            foreach (var sa in saGuids) relatedEntityIds.Add(sa.ToString());

            var relatedList = relatedEntityIds.ToList();
            q = q.Where(a => relatedList.Contains(a.EntityId) || (a.EntityType == "Client" && a.EntityId == cIdStr));
        }

        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(a => a.EntityId == entityId);
        if (userId is not null) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var uLower = userName.Trim().ToLower();
            q = q.Where(a => a.UserName != null && a.UserName.ToLower() == uLower);
        }
        if (!string.IsNullOrWhiteSpace(correlationId)) q = q.Where(a => a.CorrelationId == correlationId);
        if (action is not null) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(a =>
                a.EntityId.Contains(s) ||
                (a.Context != null && a.Context.ToLower().Contains(sLower)) ||
                (a.UserName != null && a.UserName.ToLower().Contains(sLower)));
        }

        if (_db.Database.IsPostgres())
        {
            if (fromUtc is not null) q = q.Where(a => a.OccurredAtUtc >= fromUtc);
            if (toUtc is not null) q = q.Where(a => a.OccurredAtUtc <= toUtc);
            var pgTotal = await q.CountAsync(ct);
            var pgRows = await q.OrderByDescending(a => a.OccurredAtUtc).Skip(skip).Take(take).ToListAsync(ct);
            return new ListResponse(pgTotal, skip, take, pgRows.Select(a => Map(a, includeDiff: true)).ToList());
        }

        // SQLite fallback — can't ORDER BY / compare DateTimeOffset in SQL.
        var materialised = await q.ToListAsync(ct);
        if (fromUtc is not null) materialised = materialised.Where(a => a.OccurredAtUtc >= fromUtc).ToList();
        if (toUtc is not null) materialised = materialised.Where(a => a.OccurredAtUtc <= toUtc).ToList();

        var total = materialised.Count;
        var page = materialised
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip(skip).Take(take)
            .Select(a => Map(a, includeDiff: true)).ToList();

        return new ListResponse(total, skip, take, page);
    }

    [HttpGet("export")]
    [HasPermission(Permissions.Audit.Read)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] string? userName,
        [FromQuery] string? correlationId,
        [FromQuery] AuditAction? action,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var q = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(a => a.EntityId == entityId);
        if (userId is not null) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var uLower = userName.Trim().ToLower();
            q = q.Where(a => a.UserName != null && a.UserName.ToLower() == uLower);
        }
        if (!string.IsNullOrWhiteSpace(correlationId)) q = q.Where(a => a.CorrelationId == correlationId);
        if (action is not null) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            q = q.Where(a => a.EntityId.Contains(s)
                             || (a.Context != null && a.Context.ToLower().Contains(sLower))
                             || (a.UserName != null && a.UserName.ToLower().Contains(sLower)));
        }

        List<AuditEntry> rows;
        if (_db.Database.IsPostgres())
        {
            if (fromUtc is not null) q = q.Where(a => a.OccurredAtUtc >= fromUtc);
            if (toUtc is not null) q = q.Where(a => a.OccurredAtUtc <= toUtc);
            rows = await q.OrderByDescending(a => a.OccurredAtUtc).ToListAsync(ct);
        }
        else
        {
            rows = await q.ToListAsync(ct);
            if (fromUtc is not null) rows = rows.Where(a => a.OccurredAtUtc >= fromUtc).ToList();
            if (toUtc is not null) rows = rows.Where(a => a.OccurredAtUtc <= toUtc).ToList();
            rows = rows.OrderByDescending(a => a.OccurredAtUtc).ToList();
        }

        var sb = new StringBuilder();
        sb.Append("OccurredAtUtc,Action,EntityType,EntityId,UserName,UserId,CorrelationId,IpAddress,Context,ChangedFields\n");
        foreach (var r in rows)
        {
            sb.Append(r.OccurredAtUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(r.Action.ToString())).Append(',');
            sb.Append(Csv(r.EntityType)).Append(',');
            sb.Append(Csv(r.EntityId)).Append(',');
            sb.Append(Csv(r.UserName)).Append(',');
            sb.Append(Csv(r.UserId?.ToString())).Append(',');
            sb.Append(Csv(r.CorrelationId)).Append(',');
            sb.Append(Csv(r.IpAddress)).Append(',');
            sb.Append(Csv(r.Context)).Append(',');
            sb.Append(Csv(r.ChangedFields));
            sb.Append('\n');
        }

        await _audit.RecordExportAsync("AuditEntry", "Export:AuditLog", rows.Count,
            new { entityType, entityId, userId, userName, correlationId, action, fromUtc, toUtc, search }, ct);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        var escaped = value.Replace("\"", "\"\"");
        return needsQuote ? $"\"{escaped}\"" : escaped;
    }

    [HttpGet("filter-options")]
    [HasPermission(Permissions.Audit.Read)]
    public async Task<ActionResult<FilterOptionsDto>> FilterOptions(CancellationToken ct)
    {
        var dbEntityTypes = await _db.AuditEntries.AsNoTracking()
            .Select(a => a.EntityType)
            .Distinct()
            .ToListAsync(ct);

        var defaultEntityTypes = new[]
        {
            "Client", "Branch", "ServiceFile", "Engagement",
            "StaffAssignment", "TaxRegistration", "Staff", "EntityType",
            "ServiceDefinition", "ClientGroup", "State", "ImportBatch"
        };

        var allEntityTypes = defaultEntityTypes
            .Union(dbEntityTypes)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .OrderBy(e => e)
            .ToList();

        var users = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.UserName != null)
            .Select(a => new { a.UserId, a.UserName })
            .Distinct()
            .ToListAsync(ct);

        var userOptions = users
            .Select(u => new UserOptionDto(u.UserId, u.UserName!))
            .OrderBy(u => u.UserName)
            .ToList();

        var actions = Enum.GetValues<AuditAction>()
            .Select(a => new ActionOptionDto((int)a, a.ToString()))
            .ToList();

        return new FilterOptionsDto(allEntityTypes, userOptions, actions);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Audit.Read)]
    public async Task<ActionResult<AuditEntryDetail>> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.AuditEntries.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return NotFound();
        return new AuditEntryDetail(Map(row, includeDiff: true), BuildDiff(row));
    }

    private static AuditEntryDto Map(AuditEntry a, bool includeDiff = true) => new(
        a.Id, a.OccurredAtUtc, a.EntityType, a.EntityId, a.Action,
        a.ChangedFields, a.OldValues, a.NewValues,
        a.UserId, a.UserName, a.CorrelationId, a.IpAddress, a.UserAgent, a.Context,
        includeDiff ? BuildDiff(a) : null);

    private static IReadOnlyList<FieldDiff> BuildDiff(AuditEntry a)
    {
        var changed = ParseStringArray(a.ChangedFields);
        var oldDict = ParseObjectDict(a.OldValues);
        var newDict = ParseObjectDict(a.NewValues);
        var allKeys = oldDict.Keys.Union(newDict.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        var changedSet = new HashSet<string>(changed, StringComparer.Ordinal);
        var diffs = new List<FieldDiff>(allKeys.Count);
        foreach (var k in allKeys)
        {
            oldDict.TryGetValue(k, out var ov);
            newDict.TryGetValue(k, out var nv);
            diffs.Add(new FieldDiff(k, ov, nv, changedSet.Contains(k)));
        }
        return diffs;
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null") return new();
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new(); }
        catch { return new(); }
    }

    private static Dictionary<string, string?> ParseObjectDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null") return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new();
            var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => p.Value.GetString(),
                    _ => p.Value.GetRawText(),
                };
            }
            return dict;
        }
        catch { return new(); }
    }
}
