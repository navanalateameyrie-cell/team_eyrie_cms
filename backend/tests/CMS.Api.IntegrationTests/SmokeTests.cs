using System.Net;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class SmokeTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public SmokeTests(CmsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Swagger_Document_Is_Available_In_Development()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"openapi\"");
        json.Should().Contain("/api/v1/Health");
    }

    [Fact]
    public async Task Unknown_Route_Returns_404_Not_500()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
