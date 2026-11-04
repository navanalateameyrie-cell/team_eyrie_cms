using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Auditing;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuditExplorerTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditExplorerTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task List_Filters_By_EntityType_And_Action()
    {
        var c = await AdminClientAsync();
        // Create a client + update it so there's a Create + an Update audit row.
        var et = (await (await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"AE-{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "AE", null)))
            .Content.ReadFromJsonAsync<EntityTypeDto>())!;
        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            $"Audit Client {Guid.NewGuid():N}"[..32], et.Id, null, null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var upd = await c.PutAsJsonAsync($"/api/v1/clients/{dto.Id}", new ClientsController.UpdateClientRequest(
            "Renamed Co", et.Id, null, null, null, null, dto.Version));
        upd.EnsureSuccessStatusCode();

        var list = await c.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?entityType=Client&entityId={dto.Id}");
        list!.Items.Should().OnlyContain(a => a.EntityType == "Client" && a.EntityId == dto.Id.ToString());
        list.Items.Select(a => a.Action).Should().Contain(new[] { AuditAction.Create, AuditAction.Update });

        var updatesOnly = await c.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?entityType=Client&entityId={dto.Id}&action=Update");
        updatesOnly!.Items.Should().OnlyContain(a => a.Action == AuditAction.Update);
    }

    [Fact]
    public async Task Detail_Returns_SideBySide_Diff_With_Only_Changed_Fields_Flagged()
    {
        var c = await AdminClientAsync();
        var et = (await (await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"DF-{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "DF", null)))
            .Content.ReadFromJsonAsync<EntityTypeDto>())!;
        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Diff Original", et.Id, "T Original", null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var upd = await c.PutAsJsonAsync($"/api/v1/clients/{dto.Id}", new ClientsController.UpdateClientRequest(
            "Diff Renamed", et.Id, "T Original", null, null, null, dto.Version));
        upd.EnsureSuccessStatusCode();

        var list = await c.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?entityType=Client&entityId={dto.Id}&action=Update");
        var updateRow = list!.Items.Should().ContainSingle().Which;

        var detail = await c.GetFromJsonAsync<AuditController.AuditEntryDetail>(
            $"/api/v1/audit/{updateRow.Id}");
        detail!.Diff.Should().NotBeEmpty();

        var legal = detail.Diff.Single(d => d.Field == "LegalName");
        legal.OldValue.Should().Be("Diff Original");
        legal.NewValue.Should().Be("Diff Renamed");
        legal.Changed.Should().BeTrue();

        // Trade name present on both sides but equal → shown, not flagged changed.
        var trade = detail.Diff.First(d => d.Field == "TradeName");
        trade.Changed.Should().BeFalse();
    }

    [Fact]
    public async Task ClientId_Filter_Pulls_Audit_Rows_Across_Related_Entities()
    {
        var c = await AdminClientAsync();
        var (clientId, _, stateId) = await BranchesTestFixtures.SeedClientAndMastersAsync(c);
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BranchesTestFixtures.BuildBranchRequest(stateId));
        brResp.EnsureSuccessStatusCode();

        var rows = await c.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?clientId={clientId}&take=100");
        rows!.Items.Select(x => x.EntityType).Distinct().Should().Contain("Client");
        rows.Items.Select(x => x.EntityType).Distinct().Should().Contain("Branch");
    }

    [Fact]
    public async Task Pagination_Returns_Correct_Page_And_Total()
    {
        var c = await AdminClientAsync();
        var page1 = await c.GetFromJsonAsync<AuditController.ListResponse>("/api/v1/audit?skip=0&take=2");
        page1!.Items.Count.Should().BeLessThanOrEqualTo(2);
        page1.Total.Should().BeGreaterThan(0);
        page1.Skip.Should().Be(0);
        page1.Take.Should().Be(2);
    }

    [Fact]
    public async Task FilterOptions_Includes_Default_Entity_Types_And_Enum_Actions()
    {
        var c = await AdminClientAsync();
        var opts = await c.GetFromJsonAsync<AuditController.FilterOptionsDto>("/api/v1/audit/filter-options");
        opts!.EntityTypes.Should().Contain(new[] { "Client", "Branch", "ServiceFile" });
        opts.Actions.Select(a => a.Name).Should().Contain(new[] { "Create", "Update", "Delete", "Read", "Export" });
    }

    [Fact]
    public async Task Caller_Without_Audit_Read_Gets_403()
    {
        var token = await _factory.CreateUserAndLoginAsync("audit-denied@cms.local", "Manager");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await c.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Gets_401()
    {
        var c = _factory.CreateClient();
        (await c.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
