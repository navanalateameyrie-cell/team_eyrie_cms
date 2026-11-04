namespace CMS.Application.Abstractions;

public interface IClientCodeGenerator
{
    /// <summary>Returns the next permanent 8-digit client code. Concurrent-safe.</summary>
    Task<string> NextAsync(CancellationToken ct = default);
}
