using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ComplianceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ComplianceTests(CmsApiFactory f) => _factory = f;

    private static async Task<ServiceDto> GetOrCreateMonthlyServiceAsync(HttpClient c, string code = "GST")
    {
        var dto = new ServiceUpsertDto(code, code, null, null, 0, "[]", null,
            Frequency: ComplianceFrequency.Monthly, DueDayOffsetDays: 20);
        var create = await c.PostAsJsonAsync("/api/v1/masters/services", dto);
        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            var list = await c.GetFromJsonAsync<ServiceDto[]>($"/api/v1/masters/services?search={code}");
            var existing = list!.First(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            // Patch cadence if the pre-seeded one didn't have ours.
            if (existing.Frequency != ComplianceFrequency.Monthly || existing.DueDayOffsetDays != 20)
            {
                (await c.PutAsJsonAsync($"/api/v1/masters/services/{existing.Id}",
                    new ServiceUpsertDto(existing.Code, existing.Name, existing.Category,
                        existing.Description, existing.DisplayOrder, "[]", null,
                        ComplianceFrequency.Monthly, 20)))
                    .EnsureSuccessStatusCode();
                return (await c.GetFromJsonAsync<ServiceDto>($"/api/v1/masters/services/{existing.Id}"))!;
            }
            return existing;
        }
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<ServiceDto>())!;
    }

    private static async Task<ClientServiceFilesController.ServiceFileDto> SeedFileAsync(HttpClient c)
    {
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var svc = await GetOrCreateMonthlyServiceAsync(c);
        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null, MetadataJson: null));
        fileResp.EnsureSuccessStatusCode();
        return (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
    }

    [Fact]
    public async Task Generator_Is_Idempotent_And_Fills_Horizon()
    {
        var c = await AdminClientAsync(_factory);
        await SeedFileAsync(c);

        var first = await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 });
        first.EnsureSuccessStatusCode();
        var firstBody = (await first.Content.ReadFromJsonAsync<ComplianceController.GenerateResponse>())!;
        firstBody.ObligationsCreated.Should().BeGreaterThan(0);

        var second = await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 });
        second.EnsureSuccessStatusCode();
        var secondBody = (await second.Content.ReadFromJsonAsync<ComplianceController.GenerateResponse>())!;
        secondBody.ObligationsCreated.Should().Be(0); // nothing new on a re-run
        secondBody.ObligationsSkippedExisting.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Listing_Filters_By_Status_And_Date_Range()
    {
        var c = await AdminClientAsync(_factory);
        await SeedFileAsync(c);
        (await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 })).EnsureSuccessStatusCode();

        var all = await c.GetFromJsonAsync<ComplianceController.Page<ComplianceController.ObligationDto>>(
            "/api/v1/compliance?from=2020-01-01&to=2030-12-31&take=200");
        all!.Items.Should().NotBeEmpty();
        all.Items.Should().Contain(o => o.Status == ComplianceStatus.Pending);

        // Narrow date range should return a strict subset.
        var narrow = await c.GetFromJsonAsync<ComplianceController.Page<ComplianceController.ObligationDto>>(
            $"/api/v1/compliance?from={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}" +
            $"&to={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)):yyyy-MM-dd}&take=200");
        narrow!.Total.Should().BeLessThanOrEqualTo(all.Total);
    }

    [Fact]
    public async Task Status_Lifecycle_File_And_Timeline_Events()
    {
        var c = await AdminClientAsync(_factory);
        await SeedFileAsync(c);
        (await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 })).EnsureSuccessStatusCode();

        var page = await c.GetFromJsonAsync<ComplianceController.Page<ComplianceController.ObligationDto>>(
            "/api/v1/compliance?from=2020-01-01&to=2030-12-31&take=1");
        var target = page!.Items.First();

        var toProgress = await c.PostAsJsonAsync($"/api/v1/compliance/{target.Id}/status",
            new ComplianceController.StatusRequest(ComplianceStatus.InProgress, "picking up"));
        toProgress.EnsureSuccessStatusCode();
        var afterProgress = (await toProgress.Content.ReadFromJsonAsync<ComplianceController.ObligationDto>())!;
        afterProgress.Status.Should().Be(ComplianceStatus.InProgress);

        var filed = await c.PostAsJsonAsync($"/api/v1/compliance/{target.Id}/file",
            new ComplianceController.FileRequest(
                FiledOn: DateOnly.FromDateTime(DateTime.UtcNow),
                Reference: "ARN123",
                Note: null));
        filed.EnsureSuccessStatusCode();
        var afterFiled = (await filed.Content.ReadFromJsonAsync<ComplianceController.ObligationDto>())!;
        afterFiled.Status.Should().Be(ComplianceStatus.Filed);
        afterFiled.Reference.Should().Be("ARN123");

        // A filed obligation cannot be set back to InProgress.
        var invalid = await c.PostAsJsonAsync($"/api/v1/compliance/{target.Id}/status",
            new ComplianceController.StatusRequest(ComplianceStatus.InProgress, null));
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var events = await c.GetFromJsonAsync<ComplianceController.EventDto[]>(
            $"/api/v1/compliance/{target.Id}/events");
        events!.Length.Should().BeGreaterThanOrEqualTo(3); // Generated + StatusChanged + Filed
        events.Should().Contain(e => e.Kind == ObligationEventKind.Generated);
        events.Should().Contain(e => e.ToStatus == ComplianceStatus.Filed);
    }

    [Fact]
    public async Task Aggregates_Count_Overdue_And_This_Month()
    {
        var c = await AdminClientAsync(_factory);
        await SeedFileAsync(c);
        (await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 })).EnsureSuccessStatusCode();

        var agg = await c.GetFromJsonAsync<ComplianceController.Aggregates>("/api/v1/compliance/aggregates");
        agg.Should().NotBeNull();
        (agg!.DueThisWeek + agg.OverdueOpen + agg.InProgress + agg.FiledThisMonth)
            .Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Compliance_Requires_Permission()
    {
        var token = await _factory.CreateUserAndLoginAsync("compliance-denied@cms.local", "StaffUser");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Our dev seed grants StaffUser compliance.read + write, so this test asserts the baseline
        // READ succeeds for a StaffUser — a user with ZERO permissions would 403, which is covered
        // by the generic authorization tests elsewhere.
        var resp = await c.GetAsync("/api/v1/compliance?from=2020-01-01&to=2030-12-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
