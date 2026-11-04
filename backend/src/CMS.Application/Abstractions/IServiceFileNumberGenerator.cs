namespace CMS.Application.Abstractions;

public interface IServiceFileNumberGenerator
{
    /// <summary>Returns the next permanent 10-digit service file number. Concurrent-safe.</summary>
    Task<string> NextAsync(CancellationToken ct = default);
}
