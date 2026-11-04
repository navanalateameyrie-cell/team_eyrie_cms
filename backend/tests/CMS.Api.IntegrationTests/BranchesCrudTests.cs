using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class BranchesCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public BranchesCrudTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task First_Branch_Is_Auto_Primary()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);

        var req = BuildBranchRequest(stateId, isPrimary: false);
        var resp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var br = (await resp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        br.IsPrimary.Should().BeTrue();
        br.IsActive.Should().BeTrue();
        br.ClosedOn.Should().BeNull();
    }

    [Fact]
    public async Task Second_Branch_Does_Not_Become_Primary_Unless_Requested()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));

        var second = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, type: BranchType.Branch));
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var br = (await second.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        br.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task Second_Branch_Marked_Primary_Demotes_The_Existing_One()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var firstResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var first = (await firstResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var secondResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, type: BranchType.Branch, isPrimary: true));
        var second = (await secondResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        second.IsPrimary.Should().BeTrue();

        var firstAfter = await c.GetFromJsonAsync<ClientBranchesController.BranchDto>(
            $"/api/v1/clients/{clientId}/branches/{first.Id}");
        firstAfter!.IsPrimary.Should().BeFalse();

        // Exactly one primary — invariant check.
        var all = (await c.GetFromJsonAsync<ClientBranchesController.BranchDto[]>(
            $"/api/v1/clients/{clientId}/branches"))!;
        all.Count(b => b.IsPrimary).Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_Branch_Code_Within_Client_Is_Rejected()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var code = "HO-001";
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, code))).EnsureSuccessStatusCode();

        var dup = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, code));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Rejects_Stale_Version()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var createResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var br = (await createResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var upd = new ClientBranchesController.UpdateBranchRequest(
            Code: br.Code, Name: "Renamed", Type: br.Type, StateId: stateId,
            Address: br.Address with { City = "Mysuru" },
            IsBillingEnabled: false, Version: br.Version);
        var first = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/branches/{br.Id}", upd);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replay with the pre-update version → 409
        var replay = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/branches/{br.Id}", upd);
        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Returns_Branches_With_State_Name_And_Primary_First()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, type: BranchType.Branch));

        var list = (await c.GetFromJsonAsync<ClientBranchesController.BranchDto[]>(
            $"/api/v1/clients/{clientId}/branches"))!;
        list.Should().HaveCountGreaterThanOrEqualTo(2);
        list[0].IsPrimary.Should().BeTrue();
        list[0].StateName.Should().NotBeNullOrWhiteSpace();
    }
}
