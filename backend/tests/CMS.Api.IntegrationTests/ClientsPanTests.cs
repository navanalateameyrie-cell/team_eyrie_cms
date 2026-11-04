using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class ClientsPanTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ClientsPanTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid etId)> SetupAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var et = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"PAN{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "PAN ET", null));
        var etDto = (await et.Content.ReadFromJsonAsync<EntityTypeDto>())!;
        return (c, etDto.Id);
    }

    [Fact]
    public async Task Invalid_Pan_Is_Rejected()
    {
        var (c, etId) = await SetupAsync();
        var resp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Bad PAN Co", etId, null, "NOTVALID12", null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lowercase_Pan_Input_Is_Stored_Uppercase()
    {
        var (c, etId) = await SetupAsync();
        var pan = "abcde1234f";
        var resp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Lowercase PAN", etId, null, pan, null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await resp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        dto.Pan.Should().Be(pan.ToUpperInvariant());
    }

    [Fact]
    public async Task Duplicate_Pan_Without_Acknowledgement_Returns_422_With_Candidates()
    {
        var (c, etId) = await SetupAsync();
        var pan = $"ABCDE{new Random().Next(1000, 9999)}F";

        var first = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "First PAN Owner", etId, null, pan, null, null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = (await first.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var dup = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Second PAN Owner", etId, null, pan, null, null));
        dup.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = JsonDocument.Parse(await dup.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("error").GetString().Should().Be("duplicate_pan_requires_acknowledgement");
        var candidates = body.GetProperty("candidates").EnumerateArray().ToArray();
        candidates.Should().Contain(e => e.GetProperty("id").GetGuid() == firstDto.Id);
    }

    [Fact]
    public async Task Duplicate_Pan_With_Acknowledgement_Succeeds()
    {
        var (c, etId) = await SetupAsync();
        var pan = $"XYZAB{new Random().Next(1000, 9999)}C";

        (await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "First dup-ok", etId, null, pan, null, null))).EnsureSuccessStatusCode();

        var second = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Second dup-ok", etId, null, pan, null, null, AcknowledgeDuplicatePan: true));
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DuplicatePanCandidates_Endpoint_Returns_All_Matches()
    {
        var (c, etId) = await SetupAsync();
        var pan = $"QWERT{new Random().Next(1000, 9999)}Z";

        var a = (await (await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Cand A", etId, null, pan, null, null))).Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var b = (await (await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Cand B", etId, null, pan, null, null, AcknowledgeDuplicatePan: true))).Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var list = await c.GetFromJsonAsync<ClientsController.DuplicatePanCandidate[]>(
            $"/api/v1/clients/duplicate-pan-candidates?pan={pan}");
        list!.Select(x => x.Id).Should().Contain(new[] { a.Id, b.Id });

        var listExclA = await c.GetFromJsonAsync<ClientsController.DuplicatePanCandidate[]>(
            $"/api/v1/clients/duplicate-pan-candidates?pan={pan}&excludeId={a.Id}");
        listExclA!.Select(x => x.Id).Should().NotContain(a.Id);
    }
}
