using CMS.Application.Abstractions;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.ServiceFiles;

/// <summary>
/// Shares the Phase 5 counter table ("client_code_counters") with a different row name,
/// using the same atomic UPDATE-then-read pattern — the UPDATE's row lock on Postgres and SQLite's
/// single-writer model serialise parallel callers. Not MAX(file_number)+1; holes are permanent.
/// </summary>
public class ServiceFileNumberGenerator : IServiceFileNumberGenerator
{
    public const string CounterName = "service_file";
    public const long StartingValue = 1_000_000_000; // first 10-digit number

    private readonly CmsDbContext _db;

    public ServiceFileNumberGenerator(CmsDbContext db) => _db = db;

    public async Task<string> NextAsync(CancellationToken ct = default)
        => (await NextBatchAsync(1, ct))[0];

    public async Task<IReadOnlyList<string>> NextBatchAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return Array.Empty<string>();

        var ambient = _db.Database.CurrentTransaction;
        var tx = ambient is null ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var affected = await _db.Set<CMS.Domain.Entities.ClientCodeCounter>()
                .Where(c => c.Name == CounterName)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextValue, c => c.NextValue + count), ct);

            long start;
            if (affected == 0)
            {
                _db.Set<CMS.Domain.Entities.ClientCodeCounter>()
                   .Add(new CMS.Domain.Entities.ClientCodeCounter(CounterName, StartingValue + count));
                await _db.SaveChangesAsync(ct);
                start = StartingValue;
            }
            else
            {
                var updated = await _db.Set<CMS.Domain.Entities.ClientCodeCounter>()
                                       .AsNoTracking()
                                       .Where(c => c.Name == CounterName)
                                       .Select(c => c.NextValue)
                                       .FirstAsync(ct);
                start = updated - count;
            }

            if (tx is not null) await tx.CommitAsync(ct);
            var numbers = new string[count];
            for (int i = 0; i < count; i++)
                numbers[i] = (start + i).ToString("D10");
            return numbers;
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }
}
