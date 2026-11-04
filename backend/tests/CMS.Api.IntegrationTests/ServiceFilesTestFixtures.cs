using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;

namespace CMS.Api.IntegrationTests;

internal static class ServiceFilesTestFixtures
{
    public static async Task<HttpClient> AdminClientAsync(CmsApiFactory f)
    {
        var c = f.CreateClient();
        var token = await f.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static async Task<(Guid clientId, Guid serviceId, Guid stateId, Guid? branchId)>
        SeedClientWithServiceAsync(HttpClient c, bool withBranch = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..5].ToUpper();

        var etResp = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"ET-{suffix}", $"Entity {suffix}", null));
        var et = (await etResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;

        var stResp = await c.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto($"ST{suffix}", $"State {suffix}", "IN", 0));
        var st = (await stResp.Content.ReadFromJsonAsync<StateDto>())!;

        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto($"SVC-{suffix}", $"Service {suffix}", "Compliance", null, 0));
        var svc = (await svcResp.Content.ReadFromJsonAsync<ServiceDto>())!;

        var cliResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: $"Client {suffix}", EntityTypeId: et.Id,
            TradeName: null, Pan: null, ClientGroupId: null, LegacyClientId: null));
        var client = (await cliResp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        Guid? branchId = null;
        if (withBranch)
        {
            var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{client.Id}/branches",
                BranchesTestFixtures.BuildBranchRequest(st.Id));
            var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
            branchId = br.Id;
        }

        return (client.Id, svc.Id, st.Id, branchId);
    }
}
