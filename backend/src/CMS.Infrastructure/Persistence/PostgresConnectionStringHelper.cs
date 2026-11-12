using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CMS.Infrastructure.Persistence;

/// <summary>
/// Resolves and normalises PostgreSQL connection strings across local development,
/// Docker Compose, and cloud providers (Render, Neon, Supabase) that supply a
/// <c>postgres://</c> / <c>postgresql://</c> URI via <c>DATABASE_URL</c>.
/// </summary>
public static class PostgresConnectionStringHelper
{
    public static string? Resolve(IConfiguration configuration)
    {
        // Priority:
        // 1. Explicit env override ConnectionStrings__CmsDb
        // 2. Cloud-standard DATABASE_URL env/config (Render, Neon, Supabase)
        // 3. Configured ConnectionStrings:CmsDb (appsettings / in-memory test config / user secrets)
        var explicitEnv = Environment.GetEnvironmentVariable("ConnectionStrings__CmsDb");
        var raw = !string.IsNullOrWhiteSpace(explicitEnv)
            ? explicitEnv
            : !string.IsNullOrWhiteSpace(configuration["DATABASE_URL"])
                ? configuration["DATABASE_URL"]
                : configuration.GetConnectionString("CmsDb");

        return string.IsNullOrWhiteSpace(raw) ? null : Normalize(raw);
    }

    public static string Normalize(string rawConnectionString)
    {
        var trimmed = rawConnectionString.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
        };

        var query = ParseQueryString(uri.Query);
        if (query.TryGetValue("sslmode", out var sslModeRaw) &&
            Enum.TryParse<SslMode>(sslModeRaw, ignoreCase: true, out var parsedSsl))
        {
            csb.SslMode = parsedSsl;
        }
        else if (!IsLocalHost(uri.Host))
        {
            // Render internal/external, Neon, and Supabase Postgres require SSL with self-signed certs.
            csb.SslMode = SslMode.Require;
        }

        if (query.TryGetValue("pooling", out var pooling) && bool.TryParse(pooling, out var p))
            csb.Pooling = p;

        return csb.ConnectionString;
    }

    private static bool IsLocalHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "db", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(queryString)) return result;

        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
                result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
        return result;
    }
}
