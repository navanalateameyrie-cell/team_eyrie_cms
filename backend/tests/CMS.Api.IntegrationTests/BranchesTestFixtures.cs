using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;

namespace CMS.Api.IntegrationTests;

internal static class BranchesTestFixtures
{
    public static async Task<HttpClient> AdminClientAsync(CmsApiFactory f)
    {
        var c = f.CreateClient();
        var token = await f.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static async Task<(Guid clientId, Guid entityTypeId, Guid stateId)> SeedClientAndMastersAsync(
        HttpClient c, string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N")[..5].ToUpper();

        var etResp = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"ET-{suffix}", $"Entity {suffix}", null));
        etResp.EnsureSuccessStatusCode();
        var et = (await etResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;

        var stResp = await c.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto($"ST{suffix}", $"State {suffix}", "IN", 0));
        stResp.EnsureSuccessStatusCode();
        var st = (await stResp.Content.ReadFromJsonAsync<StateDto>())!;

        var cliResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: $"Client {suffix}", EntityTypeId: et.Id,
            TradeName: null, Pan: null, ClientGroupId: null, LegacyClientId: null));
        cliResp.EnsureSuccessStatusCode();
        var client = (await cliResp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        return (client.Id, et.Id, st.Id);
    }

    public static ClientBranchesController.CreateBranchRequest BuildBranchRequest(
        Guid stateId, string? code = null, BranchType type = BranchType.HeadOffice, bool isPrimary = false)
    {
        return new ClientBranchesController.CreateBranchRequest(
            Code: code ?? $"BR-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            Name: "Head Office",
            Type: type,
            StateId: stateId,
            Address: new ClientBranchesController.AddressDto(
                Line1: "100 Main Street",
                Line2: "Suite 2",
                City: "Bengaluru",
                Pincode: "560001"),
            OpenedOn: new DateOnly(2024, 1, 1),
            IsPrimary: isPrimary,
            IsBillingEnabled: true);
    }
}
