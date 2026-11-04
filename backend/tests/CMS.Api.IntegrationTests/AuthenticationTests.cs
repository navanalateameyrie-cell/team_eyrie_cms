using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuthenticationTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuthenticationTests(CmsApiFactory f) => _factory = f;

    private static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Login_With_Valid_Credentials_Returns_Jwt_And_Permissions()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/v1/auth/login",
            JsonBody(new { email = CmsApiFactory.DevAdminEmail, password = CmsApiFactory.DevAdminPassword }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("tokenType").GetString().Should().Be("Bearer");
        doc.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("roles").EnumerateArray().Select(e => e.GetString()).Should().Contain("SystemAdmin");
        doc.RootElement.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).Should().Contain("admin.full");
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/v1/auth/login",
            JsonBody(new { email = CmsApiFactory.DevAdminEmail, password = "WrongPassword1!" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_With_Unknown_User_Returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/v1/auth/login",
            JsonBody(new { email = "nobody@cms.local", password = "Whatever1!" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_With_Token_Returns_User_Profile()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/v1/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("userName").GetString().Should().Be(CmsApiFactory.DevAdminEmail);
        doc.RootElement.GetProperty("roles").EnumerateArray().Select(e => e.GetString()).Should().Contain("SystemAdmin");
    }

    [Fact]
    public async Task Protected_Endpoint_With_Malformed_Token_Returns_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "garbage.token.value");

        var resp = await client.GetAsync("/api/v1/diagnostics/authenticated");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
