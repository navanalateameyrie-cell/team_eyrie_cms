using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class EngagementsCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public EngagementsCrudTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, Guid fileId)> SeedFileAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var r = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var file = (await r.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        return (c, clientId, file.Id);
    }

    [Fact]
    public async Task Create_Succeeds_And_Starts_In_Planned_Status()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                FinancialYear: "2023-24", AssessmentYear: "2024-25",
                PeriodType: EngagementPeriodType.Annual, PeriodLabel: null,
                PlannedStartDate: new DateOnly(2024, 4, 1),
                PlannedEndDate: new DateOnly(2024, 9, 30), Notes: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await resp.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        dto.Status.Should().Be(EngagementStatus.Planned);
        dto.FinancialYear.Should().Be("2023-24");
        dto.AssessmentYear.Should().Be("2024-25");
        dto.Version.Should().Be(1);
    }

    [Fact]
    public async Task Invalid_Financial_Year_Is_Rejected()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var cases = new[] { "2023-25", "2023-23", "202324", "2023/24" };
        foreach (var fy in cases)
        {
            var resp = await c.PostAsJsonAsync(
                $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
                new ServiceFileEngagementsController.CreateEngagementRequest(
                    fy, null, EngagementPeriodType.Annual, null,
                    new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 30), null));
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"FY '{fy}' must be rejected");
        }
    }

    [Fact]
    public async Task Duplicate_Period_For_Same_Year_And_File_Is_Rejected()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var payload = new ServiceFileEngagementsController.CreateEngagementRequest(
            "2023-24", null, EngagementPeriodType.Annual, null,
            new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30), null);
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{fileId}/engagements", payload))
            .EnsureSuccessStatusCode();

        var dup = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{fileId}/engagements", payload);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Multiple_Quarterly_Engagements_In_Same_Year_Are_Allowed()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        foreach (var q in new[] { "Q1", "Q2", "Q3", "Q4" })
        {
            var resp = await c.PostAsJsonAsync(
                $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
                new ServiceFileEngagementsController.CreateEngagementRequest(
                    "2023-24", null, EngagementPeriodType.Quarter, q,
                    new DateOnly(2023, 4, 1), new DateOnly(2024, 3, 31), null));
            resp.StatusCode.Should().Be(HttpStatusCode.Created, $"Quarter {q} should succeed");
        }
        var list = await c.GetFromJsonAsync<ServiceFileEngagementsController.EngagementDto[]>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements?financialYear=2023-24");
        list!.Should().HaveCount(4);
        list.Select(e => e.PeriodLabel).Should().BeEquivalentTo(new[] { "Q1", "Q2", "Q3", "Q4" });
    }

    [Fact]
    public async Task Create_On_Closed_File_Returns_400()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var file = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}");
        var closeResp = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/close?version={file!.Version}", null);
        closeResp.EnsureSuccessStatusCode();

        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                "2023-24", null, EngagementPeriodType.Annual, null,
                new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30), null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Planned_End_Before_Start_Is_Rejected()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var resp = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                "2023-24", null, EngagementPeriodType.Annual, null,
                new DateOnly(2024, 9, 30), new DateOnly(2024, 4, 1), null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Rejects_Stale_Version()
    {
        var (c, clientId, fileId) = await SeedFileAsync();
        var cr = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                "2023-24", null, EngagementPeriodType.Annual, null,
                new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30), null));
        var dto = (await cr.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;

        var upd = new ServiceFileEngagementsController.UpdateEngagementRequest(
            "2023-24", "2024-25", EngagementPeriodType.Annual, null,
            new DateOnly(2024, 4, 1), new DateOnly(2024, 10, 31), "edited", dto.Version);
        var first = await c.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{dto.Id}", upd);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await c.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{dto.Id}", upd);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
