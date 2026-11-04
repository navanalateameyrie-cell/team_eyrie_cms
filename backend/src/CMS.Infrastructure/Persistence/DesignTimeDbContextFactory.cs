using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CMS.Infrastructure.Persistence;

/// <summary>
/// Enables `dotnet ef migrations add` from the Infrastructure project without needing the API host.
/// Reads ConnectionStrings:CmsDb from appsettings in the API project, with env var override.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "CMS.Api"));

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(basePath) ? basePath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("CmsDb")
                   ?? "Host=localhost;Port=5432;Database=cms;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseNpgsql(conn, b => b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName))
            .Options;

        return new CmsDbContext(options);
    }
}
