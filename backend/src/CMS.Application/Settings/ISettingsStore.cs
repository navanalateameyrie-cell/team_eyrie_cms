namespace CMS.Application.Settings;

/// <summary>
/// Overlay-first configuration access. Each call returns either a DB-backed row deserialised to
/// <typeparamref name="T"/> or the IOptions default when no row exists for the given key. Readers
/// MUST NOT cache beyond a request scope — settings may change at any time and this store is the
/// single source of truth for live values.
/// </summary>
public interface ISettingsStore
{
    Task<T> GetAsync<T>(string key, CancellationToken ct = default) where T : class, new();

    /// <summary>Return the raw JSON for a key, or null if not present.</summary>
    Task<string?> GetRawAsync(string key, CancellationToken ct = default);

    /// <summary>Create or update the DB-backed row for the given key.</summary>
    Task SetAsync(string key, string valueJson, Guid? userId, string? userName, CancellationToken ct = default);

    /// <summary>List all keys currently persisted in the DB.</summary>
    Task<IReadOnlyList<(string Key, string ValueJson, DateTimeOffset? UpdatedAtUtc, string? UpdatedByUserName)>> ListAsync(CancellationToken ct = default);
}
