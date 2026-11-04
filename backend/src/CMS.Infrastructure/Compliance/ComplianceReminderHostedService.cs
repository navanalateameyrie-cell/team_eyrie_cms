using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMS.Infrastructure.Compliance;

/// <summary>
/// Fires the compliance reminder sweep once per calendar day at the configured UTC hour. Tracks
/// the last date it ran so a mid-day restart doesn't double-send. All idempotency guarantees still
/// come from <c>ReminderLog</c>'s unique index, this scheduler only picks the moment.
/// </summary>
public class ComplianceReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReminderOptions _opts;
    private readonly ILogger<ComplianceReminderHostedService> _log;
    private DateOnly? _lastRunUtcDate;

    public ComplianceReminderHostedService(IServiceScopeFactory scopeFactory,
                                            IOptions<ReminderOptions> opts,
                                            ILogger<ComplianceReminderHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
        {
            _log.LogInformation("Compliance reminders disabled via config");
            return;
        }

        // First check after a short settle delay — gives migrations/seeding time to finish.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var today = DateOnly.FromDateTime(now);
                var runHour = Math.Clamp(_opts.DailyRunHourUtc, 0, 23);
                if (_lastRunUtcDate != today && now.Hour >= runHour)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IComplianceReminderService>();
                    await svc.RunAsync(windowDaysOverride: null, dryRun: false, stoppingToken);
                    _lastRunUtcDate = today;
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Compliance reminder tick failed; will retry on next period");
            }

            // Wake every 30 minutes to re-check the hour window — cheap, resilient.
            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
