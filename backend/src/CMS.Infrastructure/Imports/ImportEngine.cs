using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using CMS.Application.Abstractions;
using CMS.Application.Clients;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Imports;

/// <summary>
/// All master writes from an uploaded sheet pass through staging. On commit we read the staging
/// rows, call the same generators / aggregates production code uses, and link the committed master
/// row back to the staging row via `CommittedClientId` / `CommittedServiceFileId`.
/// </summary>
public class ImportEngine : IImportEngine
{
    private const string ClientsSheetName = "Clients";
    private const string FinalSheetName = "Final";
    private const string ServiceColumnPrefix = "SERVICE_";
    private const string ShortServiceColumnPrefix = "SF_";

    private static readonly Dictionary<string, string> KnownServiceColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accounts"] = "ACCOUNTS",
        ["Accounting"] = "ACCOUNTS",
        ["IT"] = "IT",
        ["Income Tax"] = "IT",
        ["GST"] = "GST",
        ["AGST"] = "AGST",
        ["Annual GST"] = "AGST",
        ["TDS"] = "TDS",
        ["Certification"] = "CERTIFICATION",
        ["ITO Hearings"] = "ITOHEARINGS",
        ["ITOHearings"] = "ITOHEARINGS",
        ["CIT Appeals"] = "CITAPPEALS",
        ["CITAppeals"] = "CITAPPEALS",
        ["ITAT Appeals"] = "ITATAPPEALS",
        ["ITATAppeals"] = "ITATAPPEALS",
        ["Bank Audit"] = "BANKAUDIT",
        ["BankAudit"] = "BANKAUDIT",
        ["ROC"] = "ROC",
        ["ESI/PF"] = "ESIPF",
        ["ESIPF"] = "ESIPF",
        ["ESI"] = "ESIPF",
        ["PF"] = "ESIPF",
        ["PT"] = "PT",
        ["Professional Tax"] = "PT",
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly CmsDbContext _db;
    private readonly IClientCodeGenerator _clientCodes;
    private readonly IServiceFileNumberGenerator _fileNumbers;

    public ImportEngine(CmsDbContext db, IClientCodeGenerator clientCodes, IServiceFileNumberGenerator fileNumbers)
    {
        _db = db;
        _clientCodes = clientCodes;
        _fileNumbers = fileNumbers;
    }

    // ---- 1. Upload ----

    public async Task<UploadResult> UploadAsync(string fileName, byte[] content, Guid? userId, CancellationToken ct = default)
    {
        if (content is null || content.Length == 0) throw new ArgumentException("Empty upload", nameof(content));

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var liveCandidates = await _db.ImportBatches
            .Where(b => b.FileSha256 == hash
                     && b.Status != ImportBatchStatus.Failed
                     && b.Status != ImportBatchStatus.Cancelled
                     && !(b.Status == ImportBatchStatus.Committed
                          && b.CommittedClientCount == 0
                          && b.CommittedServiceFileCount == 0))
            .ToListAsync(ct);
        var existing = liveCandidates
            .OrderByDescending(b => b.UploadedAtUtc)
            .FirstOrDefault();
        if (existing is not null)
            return new UploadResult(existing.Id, true, hash);

        var batch = new ImportBatch(fileName, hash, content.LongLength, userId);
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return new UploadResult(batch.Id, false, hash);
    }

    // ---- 2. Parse ----

    public async Task ParseAsync(Guid batchId, byte[] content, CancellationToken ct = default)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
                    ?? throw new InvalidOperationException($"Batch {batchId} not found");
        if (batch.Status != ImportBatchStatus.Received)
            throw new InvalidOperationException($"Batch is in {batch.Status}, cannot parse again.");

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var wb = new XLWorkbook(stream);

            var sheet = wb.Worksheets.Contains(ClientsSheetName) ? wb.Worksheet(ClientsSheetName)
                     : wb.Worksheets.Contains(FinalSheetName) ? wb.Worksheet(FinalSheetName)
                     : wb.Worksheets.First();

            var header = ReadHeader(sheet);
            var hasServiceCols = header.Keys.Any(IsServiceColumn);
            var rows = sheet.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? new List<IXLRangeRow>();

            int rowCount = 0;
            foreach (var row in rows)
            {
                var cells = header.ToDictionary(
                    kv => kv.Key,
                    kv => row.Cell(kv.Value).GetString().Trim(),
                    StringComparer.OrdinalIgnoreCase);

                if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;

                var raw = JsonSerializer.Serialize(cells, JsonOpts);
                var stagingClient = new ImportStagingClient(batch.Id, row.RowNumber(), raw);
                _db.ImportStagingClients.Add(stagingClient);

                if (hasServiceCols)
                {
                    foreach (var (colName, colIndex) in header)
                    {
                        if (!IsServiceColumn(colName)) continue;
                        var val = row.Cell(colIndex).GetString().Trim();
                        if (IsInactiveOrEmpty(val)) continue;

                        var serviceCode = ServiceCodeFromColumnName(colName);
                        var legacy = IsMarker(val) ? null : val;
                        var sf = new ImportStagingServiceFile(batch.Id, stagingClient.Id,
                            row.RowNumber(), serviceCode, legacy, null);
                        _db.ImportStagingServiceFiles.Add(sf);
                    }
                }

                rowCount++;
            }

            batch.TransitionTo(ImportBatchStatus.Parsed, sheet.Name);
            batch.RecordCounts(rowCount, 0);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            batch.SetNotes($"Parse failed: {ex.Message}");
            batch.TransitionTo(ImportBatchStatus.Failed);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = sheet.RangeUsed()?.FirstRow();
        if (headerRow is null) return map;
        foreach (var c in headerRow.CellsUsed())
        {
            var name = c.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name) && !map.ContainsKey(name))
                map[name] = c.Address.ColumnNumber;
        }
        return map;
    }

    private static bool IsServiceColumn(string name)
    {
        var n = name.Trim();
        if (KnownServiceColumns.ContainsKey(n)) return true;
        var compact = n.Replace(" ", "").Replace("/", "").ToUpperInvariant();
        if (KnownServiceColumns.ContainsKey(compact)) return true;
        return compact.StartsWith(ServiceColumnPrefix, StringComparison.Ordinal)
            || compact.StartsWith(ShortServiceColumnPrefix, StringComparison.Ordinal);
    }

    private static string ServiceCodeFromColumnName(string name)
    {
        var n = name.Trim();
        if (KnownServiceColumns.TryGetValue(n, out var code)) return code;
        var compact = n.Replace(" ", "").Replace("/", "").ToUpperInvariant();
        if (KnownServiceColumns.TryGetValue(compact, out code)) return code;
        if (compact.StartsWith(ServiceColumnPrefix, StringComparison.Ordinal)) return compact[ServiceColumnPrefix.Length..];
        if (compact.StartsWith(ShortServiceColumnPrefix, StringComparison.Ordinal)) return compact[ShortServiceColumnPrefix.Length..];
        return compact;
    }

    private static bool IsInactiveOrEmpty(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return true;
        var t = v.Trim();
        return t == "0" || t == "0.0" || t == "-" || t == "—" ||
               t.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
               t.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
               t.Equals("NIL", StringComparison.OrdinalIgnoreCase) ||
               t.Equals("NONE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarker(string v)
    {
        var t = v.Trim();
        return t.Length <= 4 && (
            t.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("X", StringComparison.OrdinalIgnoreCase) ||
            t == "1" || t == "✓" ||
            t.Equals("TRUE", StringComparison.OrdinalIgnoreCase));
    }

    private static string InferEntityType(string? pan, string? legalName, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            if (s.Contains("SALARY") || s.Contains("INDIVIDUAL") || s.Contains("PROP"))
                return "INDIVIDUAL";
            if (s.Contains("PVT") || s.Contains("LTD") || s.Contains("COMPANY"))
                return "PVT_LTD";
            if (s.Contains("FIRM") || s.Contains("PARTNER"))
                return "PARTNERSHIP";
            if (s.Contains("LLP"))
                return "LLP";
            if (s.Contains("TRUST") || s.Contains("NGO"))
                return "TRUST";
        }

        if (!string.IsNullOrWhiteSpace(pan) && pan.Length == 10)
        {
            var c = char.ToUpperInvariant(pan[3]);
            switch (c)
            {
                case 'C': return "PVT_LTD";
                case 'P': return "INDIVIDUAL";
                case 'F': return "PARTNERSHIP";
                case 'A': return "PARTNERSHIP";
                case 'T': return "TRUST";
                case 'H': return "INDIVIDUAL";
                case 'B': return "INDIVIDUAL";
                case 'L': return "INDIVIDUAL";
                case 'J': return "INDIVIDUAL";
                case 'G': return "INDIVIDUAL";
            }
        }

        if (!string.IsNullOrWhiteSpace(legalName))
        {
            var upper = legalName.ToUpperInvariant();
            if (upper.Contains("PVT LTD") || upper.Contains("PRIVATE LIMITED") || upper.Contains("PVT. LTD") || upper.Contains("LIMITED") || upper.Contains("LTD"))
                return "PVT_LTD";
            if (upper.Contains("LLP") || upper.Contains("L.L.P"))
                return "LLP";
            if (upper.Contains("TRUST") || upper.Contains("FOUNDATION") || upper.Contains("SOCIETY") || upper.Contains("CHARITABLE"))
                return "TRUST";
            if (upper.Contains("& CO") || upper.Contains("AND CO") || upper.Contains("ASSOCIATES") || upper.Contains("& ASSOCIATES"))
                return "PARTNERSHIP";
        }

        return "INDIVIDUAL";
    }

    // ---- 3. Validate + match candidates ----

    public async Task ValidateAndMatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
                    ?? throw new InvalidOperationException($"Batch {batchId} not found");
        if (batch.Status != ImportBatchStatus.Parsed && batch.Status != ImportBatchStatus.Validated)
            throw new InvalidOperationException($"Batch is in {batch.Status}, cannot validate.");

        var rows = await _db.ImportStagingClients.Where(r => r.BatchId == batchId).ToListAsync(ct);
        int errors = 0;

        // Prefetch all entity type codes once for FK validation.
        var entityCodes = await _db.EntityTypes.Where(e => e.IsActive)
            .ToDictionaryAsync(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase, ct);
        var groupCodes = await _db.ClientGroups.Where(g => g.IsActive)
            .ToDictionaryAsync(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Preload existing clients for fast in-memory candidate matching
        var existingClients = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.Pan, c.LegacyClientId, LegalLower = c.LegalName.ToLower() })
            .ToListAsync(ct);

        var clientsByPan = existingClients
            .Where(c => !string.IsNullOrEmpty(c.Pan))
            .GroupBy(c => c.Pan!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var clientsByLegacyId = existingClients
            .Where(c => !string.IsNullOrEmpty(c.LegacyClientId))
            .GroupBy(c => c.LegacyClientId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var clientsByLegalName = existingClients
            .GroupBy(c => c.LegalLower)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var newGroupsToAdd = new Dictionary<string, ClientGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawJson, JsonOpts)
                       ?? new Dictionary<string, string>();

            string? rawPan = Field(dict, "PAN") ?? Field(dict, "Pan") ?? Field(dict, "PAN No") ?? Field(dict, "PAN Number");
            string? panClean = rawPan?.Replace(" ", "").Trim().ToUpperInvariant();
            string? pan = null;
            if (!string.IsNullOrEmpty(panClean) && PanValidator.IsValid(panClean))
            {
                pan = panClean;
            }

            string? legal = NormalizeOrNull(
                Field(dict, "LegalName") ?? Field(dict, "Legal Name") ??
                Field(dict, "Name") ?? Field(dict, "Client Name") ??
                Field(dict, "ClientName") ?? Field(dict, "Company Name")
            );

            string? trade = NormalizeOrNull(
                Field(dict, "TradeName") ?? Field(dict, "Trade Name") ?? Field(dict, "Trade")
            );

            // Fallback: If legal name was empty but trade name was provided in the sheet, use it as legal name
            if (string.IsNullOrWhiteSpace(legal) && !string.IsNullOrWhiteSpace(trade))
            {
                legal = trade;
            }

            string? legacy = NormalizeOrNull(
                Field(dict, "LegacyClientId") ?? Field(dict, "Legacy Client Id") ??
                Field(dict, "Legacy ID") ?? Field(dict, "Client Id") ??
                Field(dict, "Client ID") ?? Field(dict, "ClientId") ??
                Field(dict, "Code") ?? Field(dict, "Client Code")
            );

            string? statusHint = NormalizeOrNull(Field(dict, "Status"));

            string? explicitEtCode = NormalizeOrNull(
                Field(dict, "EntityTypeCode") ?? Field(dict, "Entity Type") ?? Field(dict, "EntityType")
            );

            string? etCode = explicitEtCode;
            var errs = new List<object>();

            if (!string.IsNullOrEmpty(explicitEtCode) && !entityCodes.ContainsKey(explicitEtCode))
            {
                errs.Add(new { code = "unknown_entity_type", etCode = explicitEtCode });
            }
            else if (string.IsNullOrEmpty(etCode))
            {
                etCode = InferEntityType(pan ?? panClean, legal, statusHint);
            }

            string? grpCode = NormalizeOrNull(
                Field(dict, "ClientGroupCode") ?? Field(dict, "Client Group") ??
                Field(dict, "Group ID") ?? Field(dict, "GroupID") ?? Field(dict, "Group")
            );

            if (!string.IsNullOrEmpty(grpCode) && !groupCodes.ContainsKey(grpCode))
            {
                if (!newGroupsToAdd.ContainsKey(grpCode))
                {
                    var groupName = NormalizeOrNull(Field(dict, "Group")) ?? grpCode;
                    var newGroup = new ClientGroup(grpCode, groupName);
                    newGroupsToAdd[grpCode] = newGroup;
                    _db.ClientGroups.Add(newGroup);
                }
            }

            row.SetNormalized(pan, legal, trade, legacy,
                              etCode?.ToUpperInvariant(), grpCode?.ToUpperInvariant());

            if (string.IsNullOrWhiteSpace(legal)) errs.Add(new { code = "missing_legal_name" });
            if (!string.IsNullOrEmpty(panClean) && pan is null)
            {
                errs.Add(new { code = "invalid_pan_format", pan = panClean });
            }

            // Fast candidate matching
            var candidates = new List<Guid>();
            if (pan is not null && clientsByPan.TryGetValue(pan, out var panMatches))
                candidates.AddRange(panMatches);
            if (!string.IsNullOrWhiteSpace(legacy) && clientsByLegacyId.TryGetValue(legacy, out var legMatches))
                candidates.AddRange(legMatches);
            if (!string.IsNullOrWhiteSpace(legal) && clientsByLegalName.TryGetValue(legal.ToLower(), out var nameMatches))
                candidates.AddRange(nameMatches);
            candidates = candidates.Distinct().ToList();

            row.SetValidation(JsonSerializer.Serialize(errs, JsonOpts),
                              JsonSerializer.Serialize(candidates, JsonOpts));
            if (errs.Count > 0) errors++;
        }

        if (newGroupsToAdd.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            foreach (var (code, grp) in newGroupsToAdd)
            {
                groupCodes[code] = grp.Id;
            }
        }

        // Service-file staging: validate service codes against active ServiceDefinitions.
        // If batch had no service file rows staged yet, extract them now from RawJson!
        var existingSfCount = await _db.ImportStagingServiceFiles.CountAsync(r => r.BatchId == batchId, ct);
        if (existingSfCount == 0)
        {
            foreach (var row in rows)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawJson, JsonOpts);
                if (dict is null) continue;

                foreach (var (colName, val) in dict)
                {
                    if (!IsServiceColumn(colName)) continue;
                    if (IsInactiveOrEmpty(val)) continue;

                    var serviceCode = ServiceCodeFromColumnName(colName);
                    var legacy = IsMarker(val) ? null : val;
                    var sf = new ImportStagingServiceFile(batchId, row.Id,
                        row.RowNumber, serviceCode, legacy, null);
                    _db.ImportStagingServiceFiles.Add(sf);
                }
            }
            await _db.SaveChangesAsync(ct);
        }

        var svcCodes = await _db.ServiceDefinitions.Where(s => s.IsActive)
            .Select(s => s.Code).ToListAsync(ct);
        var svcSet = new HashSet<string>(svcCodes, StringComparer.OrdinalIgnoreCase);
        var sfRows = await _db.ImportStagingServiceFiles.Where(r => r.BatchId == batchId).ToListAsync(ct);
        foreach (var sf in sfRows)
        {
            var errs = new List<object>();
            if (!svcSet.Contains(sf.ServiceCode))
                errs.Add(new { code = "unknown_service_code", serviceCode = sf.ServiceCode });
            sf.SetValidation(JsonSerializer.Serialize(errs, JsonOpts));
            if (errs.Count > 0) errors++;
        }

        batch.TransitionTo(ImportBatchStatus.Validated);
        batch.RecordCounts(rows.Count, errors);
        await _db.SaveChangesAsync(ct);
    }

    private static string? Field(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : null;

    private static string? NormalizeOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---- 4. Auto-resolve ----

    public async Task AutoResolveAsync(Guid batchId, CancellationToken ct = default)
    {
        var rows = await _db.ImportStagingClients.Where(r => r.BatchId == batchId && r.Decision == ImportDecision.Pending)
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.NormalizedLegalName) && string.IsNullOrWhiteSpace(row.NormalizedPan))
            {
                row.SetDecision(ImportDecision.Skip, null);
                continue;
            }

            var hasErrors = !string.IsNullOrWhiteSpace(row.ValidationErrorsJson) && row.ValidationErrorsJson != "[]";
            if (hasErrors) continue;

            var candidates = JsonSerializer.Deserialize<List<Guid>>(row.CandidateClientIdsJson ?? "[]", JsonOpts) ?? new();

            if (candidates.Count == 0)
                row.SetDecision(ImportDecision.CreateNew, null);
            else if (candidates.Count == 1 && !string.IsNullOrWhiteSpace(row.NormalizedPan))
                row.SetDecision(ImportDecision.UpdateExisting, candidates[0]);
            // multiple candidates stay Pending for manual review
        }
        await _db.SaveChangesAsync(ct);
    }

    // ---- 5. Commit ----

    public async Task<CommitResult> CommitAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
                    ?? throw new InvalidOperationException($"Batch {batchId} not found");
        if (batch.Status != ImportBatchStatus.Validated)
            throw new InvalidOperationException($"Batch is in {batch.Status}, cannot commit.");

        var clientRows = await _db.ImportStagingClients.Where(r => r.BatchId == batchId).ToListAsync(ct);
        var sfRows = await _db.ImportStagingServiceFiles.Where(r => r.BatchId == batchId).ToListAsync(ct);

        var entityCodes = await _db.EntityTypes.Where(e => e.IsActive)
            .ToDictionaryAsync(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase, ct);
        var groupCodes = await _db.ClientGroups.Where(g => g.IsActive)
            .ToDictionaryAsync(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);
        var svcByCode = await _db.ServiceDefinitions.Where(s => s.IsActive)
            .ToDictionaryAsync(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);

        int created = 0, updated = 0, filesCreated = 0, skipped = 0, errors = 0;

        // Npgsql's retrying execution strategy forbids user-opened transactions unless the whole
        // operation runs inside CreateExecutionStrategy().ExecuteAsync, which can be retried as a unit.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Reset counters each strategy attempt — if the strategy retries, we recount cleanly.
            created = 0; updated = 0; filesCreated = 0; skipped = 0; errors = 0;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Resolve a default entity type for CreateNew rows that don't supply one — a safety net
                // in test envs. If no master exists, we skip those rows and count them as errors.
                Guid? fallbackEntityTypeId = entityCodes.Count > 0 ? entityCodes.Values.First() : null;
                const int saveChunkSize = 250;

                var createPlans = new List<(ImportStagingClient Row, string LegalName, Guid EntityTypeId, Guid? GroupId)>();
                var updatePlans = new List<(ImportStagingClient Row, Guid ClientId)>();

                foreach (var row in clientRows)
                {
                    switch (row.Decision)
                    {
                        case ImportDecision.Skip:
                        case ImportDecision.Pending:
                            skipped++;
                            break;

                        case ImportDecision.CreateNew:
                            if (string.IsNullOrWhiteSpace(row.NormalizedLegalName)) { errors++; break; }
                            Guid? etId = row.EntityTypeCode is not null && entityCodes.TryGetValue(row.EntityTypeCode, out var fromCode)
                                ? fromCode : fallbackEntityTypeId;
                            if (etId is null) { errors++; break; }

                            Guid? grpId = null;
                            if (row.ClientGroupCode is not null && groupCodes.TryGetValue(row.ClientGroupCode, out var g))
                                grpId = g;

                            createPlans.Add((row, row.NormalizedLegalName, etId.Value, grpId));
                            break;

                        case ImportDecision.UpdateExisting:
                            if (row.DecidedClientId is null) { errors++; break; }
                            updatePlans.Add((row, row.DecidedClientId.Value));
                            break;
                    }
                }

                if (updatePlans.Count > 0)
                {
                    var updateIds = updatePlans.Select(u => u.ClientId).Distinct().ToArray();
                    var existingClients = await _db.Clients
                        .Where(c => updateIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, ct);

                    int pendingUpdates = 0;
                    foreach (var (row, decidedId) in updatePlans)
                    {
                        if (!existingClients.TryGetValue(decidedId, out var client)) { errors++; continue; }

                        var etId = row.EntityTypeCode is not null && entityCodes.TryGetValue(row.EntityTypeCode, out var fromCode)
                            ? fromCode : client.EntityTypeId;
                        Guid? grpId = client.ClientGroupId;
                        if (row.ClientGroupCode is not null && groupCodes.TryGetValue(row.ClientGroupCode, out var g))
                            grpId = g;

                        client.UpdateProfile(
                            legalName: row.NormalizedLegalName ?? client.LegalName,
                            tradeName: row.NormalizedTradeName ?? client.TradeName,
                            pan: row.NormalizedPan ?? client.Pan,
                            entityTypeId: etId,
                            clientGroupId: grpId,
                            legacyClientId: row.NormalizedLegacyClientId ?? client.LegacyClientId);
                        row.MarkCommitted(client.Id);
                        updated++;
                        pendingUpdates++;

                        if (pendingUpdates % saveChunkSize == 0)
                            await _db.SaveChangesAsync(ct);
                    }
                    if (pendingUpdates % saveChunkSize != 0)
                        await _db.SaveChangesAsync(ct);
                }

                if (createPlans.Count > 0)
                {
                    var allocatedCodes = await _clientCodes.NextBatchAsync(createPlans.Count, ct);
                    for (int i = 0; i < createPlans.Count; i++)
                    {
                        var (row, legalName, etId, grpId) = createPlans[i];
                        var client = new Client(allocatedCodes[i], legalName, etId,
                                                row.NormalizedTradeName, row.NormalizedPan, grpId,
                                                row.NormalizedLegacyClientId);
                        _db.Clients.Add(client);
                        row.MarkCommitted(client.Id);
                        created++;

                        if ((i + 1) % saveChunkSize == 0)
                            await _db.SaveChangesAsync(ct);
                    }
                    if (createPlans.Count % saveChunkSize != 0)
                        await _db.SaveChangesAsync(ct);
                }

                // State-scoped service codes that should be auto-linked to the client's primary branch
                // and have GSTIN / TAN pulled into their MetadataJson at commit time.
                var stateScopedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "GST", "AGST", "TDS", "PT", "ESIPF" };

                // Preload primary branches + active regs + existing service files once for every committed client.
                var committedClientIds = clientRows
                    .Select(r => r.CommittedClientId ?? r.DecidedClientId)
                    .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
                var primaryBranchByClient = committedClientIds.Length == 0
                    ? new Dictionary<Guid, Guid?>()
                    : await _db.Branches.AsNoTracking()
                        .Where(b => committedClientIds.Contains(b.ClientId) && b.IsPrimary && b.IsActive)
                        .Select(b => new { b.ClientId, b.Id })
                        .ToDictionaryAsync(x => x.ClientId, x => (Guid?)x.Id, ct);
                var primaryBranchIds = primaryBranchByClient.Values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                var regsByBranch = primaryBranchIds.Length == 0
                    ? new Dictionary<(Guid BranchId, Domain.Entities.TaxRegistrationType Type), string>()
                    : await _db.TaxRegistrations.AsNoTracking()
                        .Where(r => r.IsActive && primaryBranchIds.Contains(r.BranchId))
                        .ToDictionaryAsync(r => (r.BranchId, r.Type), r => r.Number, ct);
                var existingFiles = committedClientIds.Length == 0
                    ? Enumerable.Empty<(Guid ClientId, Guid ServiceDefinitionId, string? LegacyFileNumber)>().ToList()
                    : (await _db.ServiceFiles.AsNoTracking()
                        .Where(f => committedClientIds.Contains(f.ClientId))
                        .Select(f => new { f.ClientId, f.ServiceDefinitionId, f.LegacyFileNumber })
                        .ToListAsync(ct))
                        .Select(f => (f.ClientId, f.ServiceDefinitionId, f.LegacyFileNumber))
                        .ToList();

                var existingAny = new HashSet<(Guid ClientId, Guid ServiceId)>(
                    existingFiles.Select(f => (f.ClientId, f.ServiceDefinitionId)));
                var existingLegacy = new HashSet<(Guid ClientId, Guid ServiceId, string Legacy)>(
                    existingFiles.Where(f => f.LegacyFileNumber is not null)
                                 .Select(f => (f.ClientId, f.ServiceDefinitionId, f.LegacyFileNumber!)));

                var rowById = clientRows.ToDictionary(r => r.Id);
                var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                var sfPlans = new List<(ImportStagingServiceFile Row, Guid ClientId, Guid SvcId, Guid? BranchId, string? MetadataJson)>();

                // Final-sheet service-column staging rows: create a ServiceFile for the resolved client.
                foreach (var sf in sfRows)
                {
                    if (!rowById.TryGetValue(sf.SourceStagingClientId, out var parent)) { errors++; continue; }
                    var clientId = parent.CommittedClientId ?? parent.DecidedClientId;
                    if (clientId is null) { skipped++; continue; }

                    if (!svcByCode.TryGetValue(sf.ServiceCode, out var svcId)) { errors++; continue; }

                    // Skip exact duplicates — same client + service + legacy number already committed earlier.
                    var already = sf.LegacyFileNumber is null
                        ? existingAny.Contains((clientId.Value, svcId))
                        : existingLegacy.Contains((clientId.Value, svcId, sf.LegacyFileNumber));
                    if (already) { skipped++; sf.SetDecision(ImportDecision.Skip); continue; }

                    existingAny.Add((clientId.Value, svcId));
                    if (sf.LegacyFileNumber is not null)
                        existingLegacy.Add((clientId.Value, svcId, sf.LegacyFileNumber));

                    // Auto-link state-scoped services to the client's primary branch + stamp GSTIN/TAN.
                    Guid? branchId = null;
                    string? metadataJson = null;
                    if (stateScopedCodes.Contains(sf.ServiceCode)
                        && primaryBranchByClient.TryGetValue(clientId.Value, out var pb) && pb.HasValue)
                    {
                        branchId = pb.Value;
                        var meta = new Dictionary<string, string>();
                        if (regsByBranch.TryGetValue((pb.Value, Domain.Entities.TaxRegistrationType.Gstin), out var gstin))
                            meta["gstin"] = gstin;
                        if (regsByBranch.TryGetValue((pb.Value, Domain.Entities.TaxRegistrationType.Tan), out var tan))
                            meta["tan"] = tan;
                        if (meta.Count > 0)
                            metadataJson = System.Text.Json.JsonSerializer.Serialize(meta, jsonOpts);
                    }

                    sfPlans.Add((sf, clientId.Value, svcId, branchId, metadataJson));
                }

                if (sfPlans.Count > 0)
                {
                    const int sfChunkSize = 250;
                    var openedOn = DateOnly.FromDateTime(DateTime.UtcNow);
                    var allocatedNumbers = await _fileNumbers.NextBatchAsync(sfPlans.Count, ct);
                    for (int i = 0; i < sfPlans.Count; i++)
                    {
                        var (sf, clientId, svcId, branchId, metadataJson) = sfPlans[i];
                        var file = new ServiceFile(allocatedNumbers[i], clientId, svcId, branchId,
                                                   openedOn: openedOn,
                                                   legacyFileNumber: sf.LegacyFileNumber,
                                                   notes: sf.Notes);
                        if (metadataJson is not null) file.SetMetadata(metadataJson);
                        _db.ServiceFiles.Add(file);
                        sf.MarkCommitted(file.Id);
                        sf.SetDecision(ImportDecision.CreateNew);
                        filesCreated++;

                        if ((i + 1) % sfChunkSize == 0)
                            await _db.SaveChangesAsync(ct);
                    }
                    if (sfPlans.Count % sfChunkSize != 0)
                        await _db.SaveChangesAsync(ct);
                }

                batch.RecordCommit(created + updated, filesCreated);
                batch.TransitionTo(ImportBatchStatus.Committed);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                batch.SetNotes($"Commit failed: {ex.Message}");
                batch.TransitionTo(ImportBatchStatus.Failed);
                await _db.SaveChangesAsync(ct);
                throw;
            }
        });

        return new CommitResult(created, updated, filesCreated, skipped, errors);
    }

    public async Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return false;
        if (batch.CommittedClientCount > 0 || batch.CommittedServiceFileCount > 0)
            throw new InvalidOperationException("Cannot delete an import batch that has committed master records.");

        await _db.ImportStagingServiceFiles.Where(r => r.BatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.ImportStagingClients.Where(r => r.BatchId == batchId).ExecuteDeleteAsync(ct);
        _db.ImportBatches.Remove(batch);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
