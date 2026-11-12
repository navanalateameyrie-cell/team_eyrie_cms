using CMS.Application.Abstractions;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Clients;

/// <summary>
/// Concurrent-safe generator. Uses a single counter row and an atomic UPDATE in a transaction;
/// the UPDATE acquires a row lock in Postgres and serialises writes in SQLite, so parallel callers
/// never produce the same number. Not MAX(code)+1 — code holes are intentional and permanent.
/// </summary>
public class ClientCodeGenerator : IClientCodeGenerator
{
    public const string CounterName = "client";
    public const long StartingValue = 10_000_000; // first 8-digit number

    private readonly CmsDbContext _db;

    public ClientCodeGenerator(CmsDbContext db) => _db = db;

    public async Task<string> NextAsync(CancellationToken ct = default)
        => (await NextBatchAsync(1, ct))[0];

    public async Task<IReadOnlyList<string>> NextBatchAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return Array.Empty<string>();

        // Join an ambient transaction when the caller (e.g. the import engine) already opened one;
        // otherwise open our own.
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
            var codes = new string[count];
            for (int i = 0; i < count; i++)
                codes[i] = (start + i).ToString("D8");
            return codes;
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }
}
