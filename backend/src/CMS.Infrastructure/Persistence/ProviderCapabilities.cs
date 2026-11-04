using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CMS.Infrastructure.Persistence;

/// <summary>Light provider-capability probe used where we want a Postgres-native path and keep a
/// SQLite in-memory fallback for tests. SQLite can't ORDER BY or compare <see cref="DateTimeOffset"/>
/// in SQL, so Postgres gets the real query and SQLite materialises then sorts in LINQ-to-Objects.</summary>
public static class ProviderCapabilities
{
    public static bool IsPostgres(this DatabaseFacade db)
        => db.ProviderName is not null
           && db.ProviderName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
}
