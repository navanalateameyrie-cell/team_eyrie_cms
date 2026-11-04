using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class EngagementsLifecycleTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public EngagementsLifecycleTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, Guid fileId, ServiceFileEngagementsController.EngagementDto e)>
        SeedEngagementAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var fr = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var file = (await fr.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        var er = await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                "2023-24", null, EngagementPeriodType.Annual, null,
                new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30), null));
        var dto = (await er.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        return (c, clientId, file.Id, dto);
    }

    [Fact]
    public async Task Planned_To_InProgress_To_Completed_Stamps_Dates()
    {
        var (c, clientId, fileId, e) = await SeedEngagementAsync();

        var startedResp = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/start?version={e.Version}&actualStart=2024-04-15", null);
        startedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = (await startedResp.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        started.Status.Should().Be(EngagementStatus.InProgress);
        started.ActualStartDate.Should().Be(new DateOnly(2024, 4, 15));

        var doneResp = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/complete?version={started.Version}&actualEnd=2024-09-10", null);
        doneResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var done = (await doneResp.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        done.Status.Should().Be(EngagementStatus.Completed);
        done.ActualEndDate.Should().Be(new DateOnly(2024, 9, 10));
    }

    [Fact]
    public async Task Completing_Without_Start_Is_Rejected()
    {
        var (c, clientId, fileId, e) = await SeedEngagementAsync();
        var resp = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/complete?version={e.Version}", null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_From_Planned_Succeeds_And_Blocks_Later_Edits()
    {
        var (c, clientId, fileId, e) = await SeedEngagementAsync();
        var cancel = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/cancel?version={e.Version}", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = (await cancel.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        cancelled.Status.Should().Be(EngagementStatus.Cancelled);

        var edit = await c.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}",
            new ServiceFileEngagementsController.UpdateEngagementRequest(
                "2023-24", null, EngagementPeriodType.Annual, null,
                new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30), "try edit", cancelled.Version));
        edit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Completed_Cannot_Be_Cancelled_Or_Restarted()
    {
        var (c, clientId, fileId, e) = await SeedEngagementAsync();
        var started = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/start?version={e.Version}", null);
        var startedDto = (await started.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;
        var done = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/complete?version={startedDto.Version}", null);
        var doneDto = (await done.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;

        var cancel = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/cancel?version={doneDto.Version}", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var start2 = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/engagements/{e.Id}/start?version={doneDto.Version}", null);
        start2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
