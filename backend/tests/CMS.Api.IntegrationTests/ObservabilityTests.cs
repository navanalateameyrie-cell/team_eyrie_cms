using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class ObservabilityTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ObservabilityTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Metrics_Endpoint_Is_Reachable_And_Exposes_Standard_Series()
    {
        var c = _factory.CreateClient();
        // Prime a request so http_requests_received_total has at least one sample.
        await c.GetAsync("/api/v1/health");

        var resp = await c.GetAsync("/metrics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await resp.Content.ReadAsStringAsync();
        text.Should().Contain("# HELP process_cpu_seconds_total");
        text.Should().Contain("http_requests_received_total");
    }

    [Fact]
    public async Task Server_Error_Response_Carries_The_Same_CorrelationId_In_Header_And_Body()
    {
        var c = _factory.CreateClient();
        const string myId = "corr-" + "deadbeefcafe";
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/diagnostics/throw");
        req.Headers.Add("X-Correlation-Id", myId);

        var resp = await c.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        // Header mirrors the request's correlation id.
        resp.Headers.GetValues("X-Correlation-Id").Should().Contain(myId);

        // And the body carries it too, so a user screenshot alone is enough to grep logs.
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        doc.RootElement.GetProperty("correlationId").GetString().Should().Be(myId);
    }
}
