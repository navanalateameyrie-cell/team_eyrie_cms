using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class TdsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public TdsTests(CmsApiFactory f) => _factory = f;

    private async Task<Guid> SeedClientAsync(HttpClient c)
    {
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        return clientId;
    }

    [Fact]
    public async Task Creating_Entry_Computes_Tds_Amount_And_Infers_Quarter_And_Fy()
    {
        var c = await AdminClientAsync(_factory);
        var clientId = await SeedClientAsync(c);

        var r = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/entries",
            new TdsController.TdsEntryUpsertRequest(
                DeductionDate: new DateOnly(2024, 8, 15),  // Aug → Q2 of FY 2024-25
                Section: "194J",
                DeducteeName: "Acme Consulting",
                DeducteePan: "ABCDE1234F",
                GrossAmountInr: 50_000m,
                TdsRatePercent: 10m,
                Notes: "September vendor"));
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        var e = (await r.Content.ReadFromJsonAsync<TdsController.TdsEntryDto>())!;
        e.TdsAmountInr.Should().Be(5000m);
        e.Quarter.Should().Be(TdsQuarter.Q2);
        e.FinancialYear.Should().Be("2024-25");
    }

    [Fact]
    public async Task Summary_Aggregates_Per_Section_And_Quarter_And_Flags_Deposited()
    {
        var c = await AdminClientAsync(_factory);
        var clientId = await SeedClientAsync(c);

        async Task AddEntry(DateOnly date, string section, decimal gross, decimal rate)
        {
            (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/entries",
                new TdsController.TdsEntryUpsertRequest(
                    date, section, "Vendor " + section, null, gross, rate, null)))
                .EnsureSuccessStatusCode();
        }
        await AddEntry(new DateOnly(2024, 5, 1),  "194C", 100_000m, 1m);   // Q1
        await AddEntry(new DateOnly(2024, 5, 10), "194C", 50_000m,  1m);   // Q1
        await AddEntry(new DateOnly(2024, 11, 1), "194J", 20_000m, 10m);   // Q3
        await AddEntry(new DateOnly(2024, 2, 15), "194J", 10_000m, 10m);   // Q4 of FY2023-24 (not 24-25)

        // Create one challan, link both Q1 194C entries.
        var challan = (await (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/challans",
                new TdsController.TdsChallanUpsertRequest(
                    "BSR-123/2024/01", new DateOnly(2024, 7, 7), "0001234", "194C", 1500m, null))).Content
            .ReadFromJsonAsync<TdsController.TdsChallanDto>())!;
        var entries = await c.GetFromJsonAsync<TdsController.TdsEntryDto[]>(
            $"/api/v1/clients/{clientId}/tds/entries?fy=2024-25&section=194C");
        var ids = entries!.Select(e => e.Id).ToList();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/challans/{challan.Id}/link",
            new TdsController.LinkRequest(ids))).EnsureSuccessStatusCode();

        var summary = (await c.GetFromJsonAsync<TdsController.Summary>(
            $"/api/v1/clients/{clientId}/tds/summary?fy=2024-25"))!;
        summary.FinancialYear.Should().Be("2024-25");
        summary.TdsInr.Should().Be(1500m + 2000m);  // (100k+50k)*1% + 20k*10%
        summary.DepositedInr.Should().Be(1500m, "only the Q1 194C entries are linked to a challan");

        var q1_194c = summary.Grid.First(c => c.Section == "194C" && c.Quarter == TdsQuarter.Q1);
        q1_194c.Count.Should().Be(2);
        q1_194c.GrossInr.Should().Be(150_000m);
        q1_194c.TdsInr.Should().Be(1500m);
        q1_194c.DepositedInr.Should().Be(1500m);

        // FY 2023-24 entry must not appear in the 2024-25 grid.
        summary.Grid.Any(c => c.Section == "194J" && c.Quarter == TdsQuarter.Q4).Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_Challan_Number_Per_Client_Is_Rejected_409()
    {
        var c = await AdminClientAsync(_factory);
        var clientId = await SeedClientAsync(c);
        var payload = new TdsController.TdsChallanUpsertRequest(
            "SAME-01", new DateOnly(2024, 7, 7), null, "194C", 500m, null);
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/challans", payload))
            .EnsureSuccessStatusCode();
        var dup = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/tds/challans", payload);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Linking_Entries_Across_Clients_Is_Isolated()
    {
        var c = await AdminClientAsync(_factory);
        var a = await SeedClientAsync(c);
        var b = await SeedClientAsync(c);

        var entryA = (await (await c.PostAsJsonAsync($"/api/v1/clients/{a}/tds/entries",
            new TdsController.TdsEntryUpsertRequest(new DateOnly(2024, 6, 1), "194C",
                "Vendor A", null, 10_000m, 1m, null))).Content
            .ReadFromJsonAsync<TdsController.TdsEntryDto>())!;
        var challanB = (await (await c.PostAsJsonAsync($"/api/v1/clients/{b}/tds/challans",
            new TdsController.TdsChallanUpsertRequest("CH-B-1", new DateOnly(2024, 7, 7),
                null, "194C", 100m, null))).Content
            .ReadFromJsonAsync<TdsController.TdsChallanDto>())!;

        // Attempt to link client-A entry to client-B challan via client-B's URL — should link 0.
        var r = await c.PostAsJsonAsync($"/api/v1/clients/{b}/tds/challans/{challanB.Id}/link",
            new TdsController.LinkRequest(new List<Guid> { entryA.Id }));
        r.EnsureSuccessStatusCode();
        var text = await r.Content.ReadAsStringAsync();
        text.Should().Contain("\"linked\":0");

        // And the original entry is still unlinked.
        var reread = (await c.GetFromJsonAsync<TdsController.TdsEntryDto[]>(
            $"/api/v1/clients/{a}/tds/entries"))!;
        reread.First(e => e.Id == entryA.Id).ChallanId.Should().BeNull();
    }
}
