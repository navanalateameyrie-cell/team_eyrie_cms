using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public HealthEndpointTests(CmsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task V1_Health_Returns_Ok_With_Status_Payload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
        doc.RootElement.GetProperty("service").GetString().Should().Be("CMS.Api");
    }

    [Fact]
    public async Task Liveness_Probe_Returns_Healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Response_Echoes_Correlation_Id_When_Provided()
    {
        var client = _factory.CreateClient();
        const string incoming = "test-corr-123";
        client.DefaultRequestHeaders.Add("X-Correlation-Id", incoming);

        var response = await client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(incoming);
    }

    [Fact]
    public async Task Health_Response_Generates_Correlation_Id_When_Missing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }
}
