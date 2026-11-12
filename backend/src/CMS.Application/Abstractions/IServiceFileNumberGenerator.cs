namespace CMS.Application.Abstractions;

public interface IServiceFileNumberGenerator
{
    /// <summary>Returns the next permanent 10-digit service file number. Concurrent-safe.</summary>
    Task<string> NextAsync(CancellationToken ct = default);

    /// <summary>Atomically reserves <paramref name="count"/> contiguous 10-digit service file numbers.</summary>
    Task<IReadOnlyList<string>> NextBatchAsync(int count, CancellationToken ct = default);
}
