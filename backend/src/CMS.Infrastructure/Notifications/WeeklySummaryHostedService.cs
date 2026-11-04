using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Notifications;

/// <summary>
/// Runs <see cref="IWeeklySummaryService"/> once per iso-week on Monday after <see cref="RunHourUtc"/>.
/// Idempotency is enforced by the unique index on <c>weekly_summary_logs.IsoWeekStart</c> plus an
/// in-memory last-run-week guard, so mid-week restarts never double-send.
/// </summary>
public class WeeklySummaryHostedService : BackgroundService
{
    private const int RunHourUtc = 7;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklySummaryHostedService> _log;
    private DateOnly? _lastWeekRan;

    public WeeklySummaryHostedService(IServiceScopeFactory scopeFactory, ILogger<WeeklySummaryHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var today = DateOnly.FromDateTime(nowUtc);
                var weekStart = WeeklySummaryService.IsoWeekStart(today);
                var isMonday = today.DayOfWeek == DayOfWeek.Monday;
                if (isMonday && nowUtc.Hour >= RunHourUtc && _lastWeekRan != weekStart)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IWeeklySummaryService>();
                    var r = await svc.RunAsync(dryRun: false, forWeekStart: weekStart, stoppingToken);
                    _lastWeekRan = weekStart;
                    _log.LogInformation("Weekly summary ran for {Week}: alreadyRan={Already} recipients={N} sent={Sent} failed={Failed}",
                        r.IsoWeekStart, r.AlreadyRan, r.Recipients, r.EmailsSent, r.EmailsFailed);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Weekly summary tick failed; will retry on next check");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
