using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Compliance;

/// <summary>
/// Background worker that invokes the compliance generator once a day. Runs on startup after a
/// short delay (so migrations/seeding finish first) and then every 24h thereafter. Pure wall-clock
/// scheduling — no cron library required for this cadence.
/// </summary>
public class ComplianceSchedulerHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Period = TimeSpan.FromHours(24);
    private const int HorizonMonths = 6;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceSchedulerHostedService> _log;

    public ComplianceSchedulerHostedService(IServiceScopeFactory scopeFactory,
                                             ILogger<ComplianceSchedulerHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
                // Only run if the schema is live — the first dev startup may still be migrating.
                var hasTable = await db.Database.CanConnectAsync(stoppingToken);
                if (hasTable)
                {
                    var gen = scope.ServiceProvider.GetRequiredService<IComplianceObligationGenerator>();
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var result = await gen.GenerateAsync(today, HorizonMonths, actorUserId: null,
                        actorUserName: "scheduler", stoppingToken);
                    _log.LogInformation("Compliance scheduler tick: scanned={Scanned} created={Created} skipped={Skipped}",
                        result.FilesScanned, result.ObligationsCreated, result.ObligationsSkippedExisting);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Compliance scheduler tick failed; will retry on next period");
            }

            try { await Task.Delay(Period, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
