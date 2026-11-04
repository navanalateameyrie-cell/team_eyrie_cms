using CMS.Domain.Identity;
using CMS.Infrastructure.Auditing;
using CMS.Infrastructure.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.Api.IntegrationTests;

/// <summary>
/// Swaps the Npgsql context for an in-memory SQLite one, forces a fixed JWT key, and seeds the dev
/// role/permission catalog so RBAC behavior is deterministic across tests.
/// </summary>
public class CmsApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "integration-test-signing-key-must-be-at-least-32-chars-long!!";
    public const string DevAdminEmail = IdentitySeeder.DevAdminEmail;
    public const string DevAdminPassword = IdentitySeeder.DevAdminPassword;

    private readonly SqliteConnection _connection = new("Filename=:memory:");
    public string DocumentStorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "cms-tests-docs-" + Guid.NewGuid().ToString("N")[..8]);
    public string MailDropFolder { get; } = EnsureDir(
        Path.Combine(Path.GetTempPath(), "cms-tests-mail-" + Guid.NewGuid().ToString("N")[..8]));

    private static string EnsureDir(string path) { Directory.CreateDirectory(path); return path; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // Keep Development so Swagger endpoints are exposed, but suppress the host seeder;
        // we seed explicitly in ConfigureServices below.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CmsDb"] = "Host=localhost;Database=cms-test;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "CMS.Api.Tests",
                ["Jwt:Audience"] = "CMS.Client.Tests",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Dev:SkipHostSeed"] = "true",
                ["DocumentStorage:Root"] = DocumentStorageRoot,
                ["Mail:DevDropFolder"] = MailDropFolder,
                ["Mail:FromAddress"] = "noreply@tests.cms.local",
                ["Mail:FromName"] = "CMS Tests",
                // Rate limiting would break parallel integration tests; disable globally in the harness.
                // The rate-limit regression test enables it per-factory via its own override.
                ["RateLimiting:Disabled"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<CmsDbContext>) ||
                    d.ServiceType == typeof(CmsDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<CmsDbContext>((sp, o) =>
            {
                o.UseSqlite(_connection);
                o.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var sp = scope.ServiceProvider;
            var ctx = sp.GetRequiredService<CmsDbContext>();
            ctx.Database.EnsureCreated();

            var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            IdentitySeeder.SeedAsync(ctx, roles, users).GetAwaiter().GetResult();
        });
    }

    public async Task<string> LoginAsAdminAsync(HttpClient? client = null)
    {
        client ??= CreateClient();
        return await LoginAsync(client, DevAdminEmail, DevAdminPassword);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { email, password });
        var resp = await client.PostAsync("/api/v1/auth/login",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    /// <summary>Creates an ad-hoc user, assigns the given dev role, returns their JWT.</summary>
    public async Task<string> CreateUserAndLoginAsync(string email, string role)
    {
        const string password = "Str0ng!Password";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsEnabled = true
            };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join(";", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
        }
        return await LoginAsync(CreateClient(), email, password);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            try { if (Directory.Exists(DocumentStorageRoot)) Directory.Delete(DocumentStorageRoot, recursive: true); }
            catch { /* best-effort */ }
            try { if (Directory.Exists(MailDropFolder)) Directory.Delete(MailDropFolder, recursive: true); }
            catch { /* best-effort */ }
        }
        base.Dispose(disposing);
    }
}
