using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuthHardeningTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuthHardeningTests(CmsApiFactory f) => _factory = f;

    private async Task<AuthController.LoginResponse> LoginAsAdmin(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(CmsApiFactory.DevAdminEmail, CmsApiFactory.DevAdminPassword));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
    }

    [Fact]
    public async Task Login_Returns_Access_And_Refresh_Tokens()
    {
        var c = _factory.CreateClient();
        var body = await LoginAsAdmin(c);
        body.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.RefreshExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task Refresh_Rotates_And_Revokes_The_Old_Token()
    {
        var c = _factory.CreateClient();
        var first = await LoginAsAdmin(c);

        var second = await c.PostAsJsonAsync("/api/v1/auth/refresh",
            new AuthController.RefreshRequest(first.RefreshToken));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = (await second.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        secondBody.RefreshToken.Should().NotBe(first.RefreshToken);

        // Reusing the old refresh after rotation must fail — this is the brute-force defence.
        var reuse = await c.PostAsJsonAsync("/api/v1/auth/refresh",
            new AuthController.RefreshRequest(first.RefreshToken));
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Revokes_Refresh_Token()
    {
        var c = _factory.CreateClient();
        var login = await LoginAsAdmin(c);

        (await c.PostAsJsonAsync("/api/v1/auth/logout",
            new AuthController.LogoutRequest(login.RefreshToken))).EnsureSuccessStatusCode();

        var afterLogout = await c.PostAsJsonAsync("/api/v1/auth/refresh",
            new AuthController.RefreshRequest(login.RefreshToken));
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Everywhere_Revokes_All_User_Refresh_Tokens()
    {
        var c = _factory.CreateClient();
        var a = await LoginAsAdmin(c);
        var b = await LoginAsAdmin(c); // second session for the same user

        var cHttp = _factory.CreateClient();
        cHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.AccessToken);
        (await cHttp.PostAsync("/api/v1/auth/logout-everywhere", null)).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync("/api/v1/auth/refresh", new AuthController.RefreshRequest(a.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.PostAsJsonAsync("/api/v1/auth/refresh", new AuthController.RefreshRequest(b.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_Password_Returns_200_Even_For_Unknown_Email_To_Avoid_Enumeration()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new AuthController.ForgotPasswordRequest("nope-" + Guid.NewGuid().ToString("N") + "@example.com"));
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_Password_Flow_Lets_User_Set_A_New_Password_Then_Login()
    {
        var c = _factory.CreateClient();

        // Create a disposable test user we can safely reset.
        var email = $"reset-{Guid.NewGuid():N}@tests.cms.local";
        var oldPwd = "Str0ng!Password";
        await _factory.CreateUserAndLoginAsync(email, "StaffUser");

        // Request a reset. The dev FileSystemEmailSender drops an .eml containing the link.
        var before = Directory.GetFiles(_factory.MailDropFolder, "*.eml").ToHashSet();
        (await c.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new AuthController.ForgotPasswordRequest(email))).EnsureSuccessStatusCode();
        var newFiles = Directory.GetFiles(_factory.MailDropFolder, "*.eml")
            .Where(f => !before.Contains(f)).ToList();
        newFiles.Should().NotBeEmpty();

        // Extract the ?token=... value out of the dropped email body.
        var text = await File.ReadAllTextAsync(newFiles[0]);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"token=([A-Za-z0-9_\-]+)");
        match.Success.Should().BeTrue("the reset email must contain a token link");
        var token = match.Groups[1].Value;

        var newPwd = "N3w!Password";
        (await c.PostAsJsonAsync("/api/v1/auth/reset-password",
            new AuthController.ResetPasswordRequest(token, newPwd))).EnsureSuccessStatusCode();

        // Single-use enforcement.
        var reuse = await c.PostAsJsonAsync("/api/v1/auth/reset-password",
            new AuthController.ResetPasswordRequest(token, newPwd));
        reuse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Old password no longer works.
        (await c.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(email, oldPwd))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // New password works.
        (await c.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(email, newPwd))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_Carries_Security_Headers()
    {
        var c = _factory.CreateClient();
        var r = await c.GetAsync("/api/v1/health");
        r.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        r.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        r.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }
}
