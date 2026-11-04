using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class TaxRegistrationTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public TaxRegistrationTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, Guid branchId)> SeedBranchAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var branchResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches", BuildBranchRequest(stateId));
        var br = (await branchResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        return (c, clientId, br.Id);
    }

    [Fact]
    public async Task Valid_Gstin_Is_Accepted_And_Normalized_Uppercase()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        // Lowercase input; valid GSTIN: 29ABCDE1234F1Z5
        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "29abcde1234f1z5", null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await resp.Content.ReadFromJsonAsync<BranchTaxRegistrationsController.TaxRegistrationDto>())!;
        dto.Number.Should().Be("29ABCDE1234F1Z5");
    }

    [Fact]
    public async Task Invalid_Gstin_Returns_400()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "NOT-A-GSTIN", null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Valid_Tan_Is_Accepted_Invalid_Rejected()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        var ok = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Tan, "BLRA12345B", null, null));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var bad = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Tan, "BAD-TAN", null, null));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_Branch_Type_Number_Returns_409()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        var body = new BranchTaxRegistrationsController.UpsertRequest(
            TaxRegistrationType.Gstin, "29ABCDE1234F1Z5", null, null);
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations", body))
            .EnsureSuccessStatusCode();
        var dup = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations", body);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Pt_Accepts_Free_Text_Because_Format_Varies_By_State()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Pt, "KA-PT-123/XYZ", null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_Bumps_Version_Stale_Returns_409()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        var created = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "29ABCDE1234F1Z5", null, null));
        var dto = (await created.Content.ReadFromJsonAsync<BranchTaxRegistrationsController.TaxRegistrationDto>())!;

        var updBody = new BranchTaxRegistrationsController.UpsertRequest(
            TaxRegistrationType.Gstin, "29ABCDE1234F2Z4", new DateOnly(2024, 4, 1), null);
        var first = await c.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations/{dto.Id}?version={dto.Version}", updBody);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await c.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations/{dto.Id}?version={dto.Version}", updBody);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Returns_Registrations_For_Branch()
    {
        var (c, clientId, branchId) = await SeedBranchAsync();
        await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "29ABCDE1234F1Z5", null, null));
        await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Tan, "BLRA12345B", null, null));

        var list = await c.GetFromJsonAsync<BranchTaxRegistrationsController.TaxRegistrationDto[]>(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations");
        list!.Should().HaveCount(2);
        list!.Select(x => x.Type).Should().Contain(new[] { TaxRegistrationType.Gstin, TaxRegistrationType.Tan });
    }
}
