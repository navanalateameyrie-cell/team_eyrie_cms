using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Auditing;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class ExportsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ExportsTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static List<string[]> ReadSheet(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();
        var rows = new List<string[]>();
        foreach (var row in ws.RangeUsed()!.RowsUsed())
        {
            var cells = row.CellsUsed(XLCellsUsedOptions.AllContents).ToList();
            // Use columns in header's range to preserve column alignment.
            var cols = ws.ColumnsUsed().Count();
            var arr = new string[cols];
            for (int i = 0; i < cols; i++) arr[i] = row.Cell(i + 1).GetString();
            rows.Add(arr);
        }
        return rows;
    }

    private async Task<(HttpClient c, Guid etId)> SeedEntityType()
    {
        var c = await AdminClientAsync();
        var et = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"EXP-{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "Export ET", null));
        var dto = (await et.Content.ReadFromJsonAsync<EntityTypeDto>())!;
        return (c, dto.Id);
    }

    [Fact]
    public async Task Export_Clients_Returns_Xlsx_With_Filter_Applied_And_Audit_Row_Written()
    {
        var (c, etId) = await SeedEntityType();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var legal = $"ExportMe {suffix}";
        var createResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            legal, etId, null, null, null, null));
        createResp.EnsureSuccessStatusCode();

        var resp = await c.GetAsync($"/api/v1/exports/clients.xlsx?search={suffix}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        var rows = ReadSheet(bytes);
        rows.Count.Should().Be(2); // 1 header + 1 data
        rows[0][1].Should().Be("Legal Name");
        rows[1][1].Should().Be(legal);

        // Audit row written via IAuditService.RecordExportAsync.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var audit = await db.AuditEntries.Where(a => a.Context == "Export:Clients"
                                                  && a.Action == AuditAction.Export)
            .ToListAsync();
        audit.Should().NotBeEmpty();
        audit.Last().NewValues.Should().Contain(suffix);
    }

    [Fact]
    public async Task Export_Clients_Honors_Active_Filter_And_Row_Count_Audit()
    {
        var (c, etId) = await SeedEntityType();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();

        // Two clients: one active, one that we'll deactivate.
        var cr1 = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            $"Active {suffix}", etId, null, null, null, null));
        var active = (await cr1.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var cr2 = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            $"Inactive {suffix}", etId, null, null, null, null));
        var inactive = (await cr2.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var deact = await c.PostAsync($"/api/v1/clients/{inactive.Id}/deactivate?version={inactive.Version}", null);
        deact.EnsureSuccessStatusCode();

        var activeOnly = await c.GetAsync($"/api/v1/exports/clients.xlsx?search={suffix}&active=true");
        var all = await c.GetAsync($"/api/v1/exports/clients.xlsx?search={suffix}");

        var activeRows = ReadSheet(await activeOnly.Content.ReadAsByteArrayAsync());
        var allRows = ReadSheet(await all.Content.ReadAsByteArrayAsync());
        activeRows.Count.Should().Be(1 + 1); // header + 1
        allRows.Count.Should().Be(1 + 2);     // header + 2
    }

    [Fact]
    public async Task Caller_Without_Exports_Perform_Gets_403()
    {
        // StaffUser has clients.read but no exports.perform.
        var token = await _factory.CreateUserAndLoginAsync("export-denied@cms.local", "StaffUser");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await c.GetAsync("/api/v1/exports/clients.xlsx")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Gets_401()
    {
        var c = _factory.CreateClient();
        (await c.GetAsync("/api/v1/exports/clients.xlsx")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
