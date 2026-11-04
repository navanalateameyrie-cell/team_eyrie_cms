using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

/// <summary>
/// Phase 8 demonstrator: one "Bank Audit" service file holds four historical yearly engagements.
/// Prior years are preserved untouched when the next year is added.
/// </summary>
public class EngagementsBankAuditScenarioTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public EngagementsBankAuditScenarioTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, Guid bankAuditFileId)> SeedBankAuditFileAsync()
    {
        var c = await AdminClientAsync(_factory);
        var suffix = Guid.NewGuid().ToString("N")[..5].ToUpper();

        var etResp = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"BK-{suffix}", $"Bank {suffix}", null));
        var et = (await etResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;

        // Dedicated "Bank Audit" service master row.
        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto($"BANK-AUDIT-{suffix}", "Bank Audit", "Audit", "Statutory audit of a bank branch", 0));
        var svc = (await svcResp.Content.ReadFromJsonAsync<ServiceDto>())!;

        var cliResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: $"ACME Bank Ltd {suffix}", EntityTypeId: et.Id,
            TradeName: null, Pan: null, ClientGroupId: null, LegacyClientId: null));
        var client = (await cliResp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{client.Id}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2021, 4, 1), null, "Bank audit engagement — one file, many years"));
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        return (c, client.Id, file.Id);
    }

    [Fact]
    public async Task Four_Historical_Years_Of_Bank_Audit_Coexist_Without_Overwriting_Each_Other()
    {
        var (c, clientId, fileId) = await SeedBankAuditFileAsync();

        var years = new[]
        {
            ("2021-22", "2022-23", new DateOnly(2022, 4, 1),  new DateOnly(2022, 5, 15)),
            ("2022-23", "2023-24", new DateOnly(2023, 4, 1),  new DateOnly(2023, 5, 15)),
            ("2023-24", "2024-25", new DateOnly(2024, 4, 1),  new DateOnly(2024, 5, 15)),
            ("2024-25", "2025-26", new DateOnly(2025, 4, 1),  new DateOnly(2025, 5, 15)),
        };

        var created = new List<ServiceFileEngagementsController.EngagementDto>();
        foreach (var (fy, ay, start, end) in years)
        {
            var resp = await c.PostAsJsonAsync(
                $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
                new ServiceFileEngagementsController.CreateEngagementRequest(
                    FinancialYear: fy, AssessmentYear: ay,
                    PeriodType: EngagementPeriodType.Annual, PeriodLabel: null,
                    PlannedStartDate: start, PlannedEndDate: end,
                    Notes: $"Statutory bank audit for FY {fy}"));
            resp.StatusCode.Should().Be(HttpStatusCode.Created,
                $"adding FY {fy} must not fail — new period = new row");
            created.Add((await resp.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!);
        }

        // All four rows are distinct, under the SAME service file, with their own ids and versions.
        created.Select(e => e.Id).Distinct().Should().HaveCount(4);
        created.Should().OnlyContain(e => e.ServiceFileId == fileId);
        created.Should().OnlyContain(e => e.Version == 1);
        created.Should().OnlyContain(e => e.Status == EngagementStatus.Planned);

        // The 2021-22 row survives after 2024-25 is added (no overwrite).
        var oldest = created.First(e => e.FinancialYear == "2021-22");
        var fetched = await c.GetFromJsonAsync<ServiceFileEngagementsController.EngagementDto>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{oldest.Id}");
        fetched!.FinancialYear.Should().Be("2021-22");
        fetched.AssessmentYear.Should().Be("2022-23");

        // Listing the file returns all four years, most-recent first (ordered by FY desc).
        var list = await c.GetFromJsonAsync<ServiceFileEngagementsController.EngagementDto[]>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements");
        list!.Should().HaveCount(4);
        list.Select(e => e.FinancialYear).Should().ContainInOrder("2024-25", "2023-24", "2022-23", "2021-22");

        // Completing an earlier year must not touch later years.
        var fy2021 = list.First(e => e.FinancialYear == "2021-22");
        var started = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{fy2021.Id}/start?version={fy2021.Version}&actualStart=2022-04-05", null);
        var startedDto = (await started.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        var completed = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{fy2021.Id}/complete?version={startedDto.Version}&actualEnd=2022-05-20", null);
        completed.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterCompletion = await c.GetFromJsonAsync<ServiceFileEngagementsController.EngagementDto[]>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements");
        afterCompletion!.Single(e => e.FinancialYear == "2021-22").Status.Should().Be(EngagementStatus.Completed);
        afterCompletion.Where(e => e.FinancialYear != "2021-22")
            .Should().OnlyContain(e => e.Status == EngagementStatus.Planned,
                "completing one year must not perturb other years' state");
    }

    [Fact]
    public async Task Reposting_The_Same_Year_Returns_409_So_Prior_Row_Is_Never_Replaced()
    {
        var (c, clientId, fileId) = await SeedBankAuditFileAsync();
        var payload = new ServiceFileEngagementsController.CreateEngagementRequest(
            "2023-24", "2024-25", EngagementPeriodType.Annual, null,
            new DateOnly(2024, 4, 1), new DateOnly(2024, 5, 15), "first write");
        var first = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements", payload);
        first.EnsureSuccessStatusCode();
        var firstDto = (await first.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;

        var retry = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements",
            payload with { Notes = "second write - must NOT replace the first" });
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Confirm original row's notes survive unchanged.
        var still = await c.GetFromJsonAsync<ServiceFileEngagementsController.EngagementDto>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{firstDto.Id}");
        still!.Notes.Should().Be("first write");
    }
}
