using System.Text;
using CMS.Application.Email;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Compliance;

/// <summary>
/// Scans open compliance obligations assigned to a staff member and dispatches ONE digest email
/// per assignee per kind (upcoming / overdue) per calendar day. Idempotency is enforced by the
/// unique index on <c>ReminderLog(ObligationId, Kind, SentOnDate)</c> plus a short-circuit check
/// before build.
/// </summary>
public interface IComplianceReminderService
{
    Task<ReminderRunResult> RunAsync(int? windowDaysOverride, bool dryRun, CancellationToken ct = default);
}

public record ReminderRunResult(
    int ScannedObligations,
    int Assignees,
    int Upcoming,
    int Overdue,
    int EmailsSent,
    int EmailsSkipped,
    bool DryRun);

public class ComplianceReminderService : IComplianceReminderService
{
    private readonly CmsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISettingsStore _settings;
    private readonly ILogger<ComplianceReminderService> _log;
    private MailOptions _mail = new();
    private ReminderOptions _opts = new();

    public ComplianceReminderService(CmsDbContext db, IEmailSender email,
                                      ISettingsStore settings,
                                      ILogger<ComplianceReminderService> log)
    {
        _db = db;
        _email = email;
        _settings = settings;
        _log = log;
    }

    public async Task<ReminderRunResult> RunAsync(int? windowDaysOverride, bool dryRun, CancellationToken ct = default)
    {
        _mail = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
        _opts = await _settings.GetAsync<ReminderOptions>(SettingKeys.Reminders, ct);
        var windowDays = Math.Clamp(windowDaysOverride ?? _opts.WindowDays, 1, 90);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowEnd = today.AddDays(windowDays);

        // Only reach obligations that are open AND assigned to someone with an email on file.
        var rows = await (from o in _db.ComplianceObligations.AsNoTracking()
                          where o.AssignedStaffId != null
                                && o.Status != ComplianceStatus.Filed
                                && o.Status != ComplianceStatus.Waived
                                && o.DueDate <= windowEnd
                          join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                          join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                          join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                          join st in _db.Staff.AsNoTracking() on o.AssignedStaffId equals st.Id
                          where st.IsActive && st.Email != null && st.Email != ""
                          select new {
                              ObligationId = o.Id,
                              o.AssignedStaffId,
                              StaffEmail = st.Email,
                              StaffName = st.FullName,
                              o.PeriodKey, o.DueDate,
                              ClientName = c.LegalName, ClientCode = c.Code,
                              ServiceCode = s.Code, ServiceName = s.Name,
                              FileNumber = f.FileNumber
                          }).ToListAsync(ct);

        // Rows the de-dupe log already covered for today — skip to keep same-day re-runs cheap.
        var obligationIds = rows.Select(r => r.ObligationId).Distinct().ToArray();
        var alreadySent = await _db.ReminderLogs.AsNoTracking()
            .Where(l => l.SentOnDate == today && obligationIds.Contains(l.ObligationId))
            .Select(l => new { l.ObligationId, l.Kind })
            .ToListAsync(ct);
        var alreadySet = alreadySent.Select(x => (x.ObligationId, x.Kind)).ToHashSet();

        int upcoming = 0, overdue = 0, sent = 0, skipped = 0;

        // Group per assignee, then split into overdue + upcoming buckets for the digest.
        var byAssignee = rows
            .GroupBy(r => new { r.AssignedStaffId, r.StaffEmail, r.StaffName })
            .ToList();

        foreach (var g in byAssignee)
        {
            if (ct.IsCancellationRequested) break;
            var overdueItems = g.Where(x => x.DueDate < today).OrderBy(x => x.DueDate).ToList();
            var upcomingItems = g.Where(x => x.DueDate >= today && x.DueDate <= windowEnd)
                                 .OrderBy(x => x.DueDate).ToList();
            overdue += overdueItems.Count;
            upcoming += upcomingItems.Count;

            // Fire two separate digests so same-day retries can succeed on one even if the other failed.
            if (overdueItems.Count > 0)
                await DispatchDigestAsync(g.Key.AssignedStaffId!.Value, g.Key.StaffEmail!, g.Key.StaffName,
                    ReminderKind.Overdue, overdueItems, today, dryRun, alreadySet, ct,
                    sentCounter: v => sent += v, skippedCounter: v => skipped += v);
            if (upcomingItems.Count > 0)
                await DispatchDigestAsync(g.Key.AssignedStaffId!.Value, g.Key.StaffEmail!, g.Key.StaffName,
                    ReminderKind.UpcomingDue, upcomingItems, today, dryRun, alreadySet, ct,
                    sentCounter: v => sent += v, skippedCounter: v => skipped += v);
        }

        _log.LogInformation("Compliance reminders: scanned={Scanned} assignees={Assignees} up={Up} over={Over} sent={Sent} skipped={Skipped} dryRun={Dry}",
            rows.Count, byAssignee.Count, upcoming, overdue, sent, skipped, dryRun);
        return new ReminderRunResult(rows.Count, byAssignee.Count, upcoming, overdue, sent, skipped, dryRun);
    }

    private async Task DispatchDigestAsync(Guid staffId, string email, string staffName,
                                            ReminderKind kind, IReadOnlyList<dynamic> items, DateOnly today,
                                            bool dryRun, HashSet<(Guid, ReminderKind)> alreadySet,
                                            CancellationToken ct,
                                            Action<int> sentCounter, Action<int> skippedCounter)
    {
        // Keep only items that haven't already got a log row for this kind today.
        var fresh = items.Where(x => !alreadySet.Contains(((Guid)x.ObligationId, kind))).ToList();
        if (fresh.Count == 0) { skippedCounter(items.Count); return; }

        var subject = kind == ReminderKind.Overdue
            ? $"[CMS] {fresh.Count} overdue compliance item(s)"
            : $"[CMS] {fresh.Count} compliance item(s) due within the next {_opts.WindowDays} day(s)";
        var body = BuildBody(staffName, kind, fresh, today);

        if (dryRun)
        {
            skippedCounter(fresh.Count);
            return;
        }

        Guid? outboxId = null;
        var outbox = new EmailOutboxEntry(
            toAddresses: email, ccAddresses: null,
            subject: subject, bodyPlainText: body,
            fromAddress: _mail.FromAddress, fromName: _mail.FromName,
            attachmentCount: 0, attachmentTotalBytes: 0,
            invoiceId: null,
            senderUserId: null, senderUserName: "scheduler");
        _db.EmailOutbox.Add(outbox);
        await _db.SaveChangesAsync(ct);
        outboxId = outbox.Id;

        try
        {
            await _email.SendAsync(
                new EmailMessage(new[] { email }, Array.Empty<string>(), subject, body, Array.Empty<EmailAttachment>()),
                ct);
            outbox.MarkSent();
            foreach (var x in fresh)
            {
                _db.ReminderLogs.Add(new ReminderLog((Guid)x.ObligationId, kind, today, staffId, email, outboxId));
                alreadySet.Add(((Guid)x.ObligationId, kind));
            }
            await _db.SaveChangesAsync(ct);
            sentCounter(fresh.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Reminder digest to {Email} failed", email);
            outbox.MarkFailed(ex.Message);
            await _db.SaveChangesAsync(ct);
            // De-dupe log NOT written: the next run can retry.
            skippedCounter(fresh.Count);
        }
    }

    private static string BuildBody(string staffName, ReminderKind kind, IReadOnlyList<dynamic> items, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.Append("Hi ").Append(staffName).Append(",\r\n\r\n");
        sb.Append(kind == ReminderKind.Overdue
            ? "The following compliance obligations assigned to you are OVERDUE:"
            : "The following compliance obligations assigned to you are due soon:");
        sb.Append("\r\n\r\n");
        foreach (var x in items)
        {
            var days = ((DateOnly)x.DueDate).DayNumber - today.DayNumber;
            var when = days < 0 ? $"{-days}d overdue"
                     : days == 0 ? "due today"
                     : $"due in {days}d";
            sb.Append("  • ").Append(x.DueDate).Append("  ").Append(when).Append("  ·  ")
              .Append(x.ClientName).Append(" (").Append(x.ClientCode).Append(")  ·  ")
              .Append(x.ServiceCode).Append("  ·  ")
              .Append(x.PeriodKey).Append("  ·  file #").Append(x.FileNumber).Append("\r\n");
        }
        sb.Append("\r\nOpen the Compliance Calendar in CMS to update statuses.\r\n");
        return sb.ToString();
    }
}
