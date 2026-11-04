using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>De-dupe row for the weekly-summary job. One per (IsoWeekStart) — the scheduler relies on
/// the unique index to make same-week re-runs cheap.</summary>
public class WeeklySummaryLog : Entity
{
    public DateOnly IsoWeekStart { get; private set; }
    public int RecipientCount { get; private set; }
    public int EmailsSent { get; private set; }
    public int EmailsFailed { get; private set; }

    private WeeklySummaryLog() { }

    public WeeklySummaryLog(DateOnly isoWeekStart, int recipientCount, int emailsSent, int emailsFailed)
    {
        IsoWeekStart = isoWeekStart;
        RecipientCount = recipientCount;
        EmailsSent = emailsSent;
        EmailsFailed = emailsFailed;
    }
}
