using System.Text;
using CMS.Application.Authorization;
using CMS.Application.Email;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Notifications;

public interface IWeeklySummaryService
{
    Task<WeeklySummaryRunResult> RunAsync(bool dryRun, DateOnly? forWeekStart, CancellationToken ct = default);
}

public record WeeklySummaryRunResult(DateOnly IsoWeekStart, bool AlreadyRan, int Recipients, int EmailsSent, int EmailsFailed, bool DryRun);

/// <summary>
/// Composes the Monday-morning digest: this week's compliance load + outstanding + revenue, writes
/// an in-app <see cref="Notification"/> for every reports-permissioned user, and emails the same
/// content via the Phase 22 sender. Idempotent per iso-week via a unique index on
/// <c>weekly_summary_logs.IsoWeekStart</c>.
/// </summary>
public class WeeklySummaryService : IWeeklySummaryService
{
    private readonly CmsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISettingsStore _settings;
    private readonly ILogger<WeeklySummaryService> _log;

    public WeeklySummaryService(CmsDbContext db, IEmailSender email, ISettingsStore settings,
                                 ILogger<WeeklySummaryService> log)
    {
        _db = db;
        _email = email;
        _settings = settings;
        _log = log;
    }

    public async Task<WeeklySummaryRunResult> RunAsync(bool dryRun, DateOnly? forWeekStart, CancellationToken ct = default)
    {
        var week = forWeekStart ?? IsoWeekStart(DateOnly.FromDateTime(DateTime.UtcNow));
        var already = await _db.WeeklySummaryLogs.AsNoTracking().AnyAsync(l => l.IsoWeekStart == week, ct);
        if (already) return new WeeklySummaryRunResult(week, true, 0, 0, 0, dryRun);

        // Build the shared digest body once from live DB numbers — the heavy lifting is the three
        // aggregate queries, not per-user composition.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var dueThisWeek = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.DueDate >= today && o.DueDate <= weekEnd
                             && o.Status != ComplianceStatus.Filed && o.Status != ComplianceStatus.Waived, ct);
        var overdue = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.DueDate < today
                             && o.Status != ComplianceStatus.Filed && o.Status != ComplianceStatus.Waived, ct);

        var openInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartPaid)
            .Select(i => new { i.TotalInr, i.PaidInr, i.CreditedInr }).ToListAsync(ct);
        var outstanding = Math.Round(openInvoices.Sum(i => i.TotalInr - i.PaidInr - i.CreditedInr), 2);

        var subject = $"[CMS] Weekly summary — week of {week:yyyy-MM-dd}";
        var body = Compose(week, dueThisWeek, overdue, outstanding);

        // Recipients: users whose role grants reports.read (we leverage the role_permissions table).
        var permRoles = await _db.RolePermissions.AsNoTracking()
            .Where(rp => rp.Permission == Permissions.Reports.Read || rp.Permission == Permissions.AdminFull)
            .Select(rp => rp.RoleId).Distinct().ToListAsync(ct);
        var userRoleRows = await _db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().AsNoTracking()
            .Where(ur => permRoles.Contains(ur.RoleId))
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsEnabled && userRoleRows.Contains(u.Id)
                        && u.ClientId == null)  // never spam external portal users
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        if (dryRun)
            return new WeeklySummaryRunResult(week, false, users.Count, 0, 0, true);

        var mail = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
        int sent = 0, failed = 0;
        foreach (var u in users)
        {
            _db.Notifications.Add(new Notification(u.Id, "weekly_summary", subject, body, link: "/admin/reports"));
            if (!string.IsNullOrWhiteSpace(u.Email))
            {
                try
                {
                    await _email.SendAsync(new EmailMessage(
                        new[] { u.Email! }, Array.Empty<string>(), subject, body, Array.Empty<EmailAttachment>()), ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _log.LogWarning(ex, "Weekly summary email to {Email} failed", u.Email);
                }
            }
        }

        _db.WeeklySummaryLogs.Add(new WeeklySummaryLog(week, users.Count, sent, failed));
        await _db.SaveChangesAsync(ct);
        return new WeeklySummaryRunResult(week, false, users.Count, sent, failed, false);
    }

    private static string Compose(DateOnly week, int dueThisWeek, int overdue, decimal outstanding)
    {
        var sb = new StringBuilder();
        sb.Append("Week of ").Append(week.ToString("dd MMM yyyy")).Append("\r\n\r\n");
        sb.Append("Compliance\r\n");
        sb.Append("  Due this week:  ").Append(dueThisWeek).Append("\r\n");
        sb.Append("  Overdue (open): ").Append(overdue).Append("\r\n\r\n");
        sb.Append("Billing\r\n");
        sb.Append("  Outstanding:    ₹").Append(outstanding.ToString("N2")).Append("\r\n\r\n");
        sb.Append("Open the Reports page in CMS for the full dashboard.\r\n");
        return sb.ToString();
    }

    /// <summary>Monday-anchored ISO week start for a given date.</summary>
    public static DateOnly IsoWeekStart(DateOnly d)
    {
        var dow = (int)d.DayOfWeek;
        var delta = dow == 0 ? 6 : dow - 1;
        return d.AddDays(-delta);
    }
}
