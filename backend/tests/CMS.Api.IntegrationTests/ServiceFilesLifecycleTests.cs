using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ServiceFilesLifecycleTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ServiceFilesLifecycleTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, ClientServiceFilesController.ServiceFileDto file)>
        SeedFileAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var r = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var dto = (await r.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        return (c, clientId, dto);
    }

    [Fact]
    public async Task Open_To_Hold_To_Open_To_Closed_To_Reopen_Preserves_FileNumber()
    {
        var (c, clientId, file) = await SeedFileAsync();
        var original = file.FileNumber;

        var hold = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/put-on-hold?version={file.Version}", null);
        hold.StatusCode.Should().Be(HttpStatusCode.OK);
        var held = (await hold.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        held.Status.Should().Be(ServiceFileStatus.OnHold);
        held.FileNumber.Should().Be(original);

        var resume = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/resume?version={held.Version}", null);
        var resumed = (await resume.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        resumed.Status.Should().Be(ServiceFileStatus.Open);

        var close = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/close?version={resumed.Version}", null);
        var closed = (await close.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        closed.Status.Should().Be(ServiceFileStatus.Closed);
        closed.ClosedOn.Should().NotBeNull();
        closed.FileNumber.Should().Be(original);

        var reopen = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/reopen?version={closed.Version}", null);
        var reopened = (await reopen.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        reopened.Status.Should().Be(ServiceFileStatus.Open);
        reopened.ClosedOn.Should().BeNull();
        reopened.FileNumber.Should().Be(original);
    }

    [Fact]
    public async Task Hold_Then_Close_Is_Allowed_But_Hold_From_Closed_Returns_400()
    {
        var (c, clientId, file) = await SeedFileAsync();
        var close = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/close?version={file.Version}", null);
        var closed = (await close.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var hold = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/put-on-hold?version={closed.Version}", null);
        hold.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_On_Closed_File_Returns_400()
    {
        var (c, clientId, file) = await SeedFileAsync();
        var close = await c.PostAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/close?version={file.Version}", null);
        var closed = (await close.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var upd = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{file.Id}",
            new ClientServiceFilesController.UpdateServiceFileRequest(
                file.ServiceDefinitionId, null, null, "try edit", closed.Version));
        upd.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
