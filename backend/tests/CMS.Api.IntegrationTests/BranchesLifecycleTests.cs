using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class BranchesLifecycleTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public BranchesLifecycleTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Closing_The_Only_Branch_Leaves_Client_Active()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var create = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var br = (await create.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var close = await c.PostAsync($"/api/v1/clients/{clientId}/branches/{br.Id}/close?version={br.Version}", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = (await close.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        closed.IsActive.Should().BeFalse();
        closed.ClosedOn.Should().NotBeNull();

        // Client itself must remain active — that's the hard invariant for this phase.
        var client = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{clientId}");
        client!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Closing_Primary_With_Other_Active_Branches_Requires_Replacement()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var primaryResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var primary = (await primaryResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, type: BranchType.Branch));

        var close = await c.PostAsync($"/api/v1/clients/{clientId}/branches/{primary.Id}/close?version={primary.Version}", null);
        close.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reopen_Restores_Active_Status_But_Not_Primary()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var createResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var br = (await createResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var close = await c.PostAsync($"/api/v1/clients/{clientId}/branches/{br.Id}/close?version={br.Version}", null);
        var closed = (await close.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var reopen = await c.PostAsync($"/api/v1/clients/{clientId}/branches/{br.Id}/reopen?version={closed.Version}", null);
        reopen.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopened = (await reopen.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        reopened.IsActive.Should().BeTrue();
        reopened.ClosedOn.Should().BeNull();
    }

    [Fact]
    public async Task Updating_Closed_Branch_Returns_400()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var createResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var br = (await createResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var close = await c.PostAsync($"/api/v1/clients/{clientId}/branches/{br.Id}/close?version={br.Version}", null);
        var closed = (await close.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var upd = new ClientBranchesController.UpdateBranchRequest(
            Code: br.Code, Name: "Try edit", Type: br.Type, StateId: stateId,
            Address: br.Address, IsBillingEnabled: true, Version: closed.Version);
        var resp = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/branches/{br.Id}", upd);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MakePrimary_Promotes_New_Branch_And_Allows_Previous_To_Be_Closed()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);

        var aResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var a = (await aResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var bResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, type: BranchType.Branch));
        var b = (await bResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var promote = await c.PostAsync(
            $"/api/v1/clients/{clientId}/branches/{b.Id}/make-primary?version={b.Version}", null);
        promote.StatusCode.Should().Be(HttpStatusCode.OK);

        var aNow = await c.GetFromJsonAsync<ClientBranchesController.BranchDto>(
            $"/api/v1/clients/{clientId}/branches/{a.Id}");
        var close = await c.PostAsync(
            $"/api/v1/clients/{clientId}/branches/{a.Id}/close?version={aNow!.Version}", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
