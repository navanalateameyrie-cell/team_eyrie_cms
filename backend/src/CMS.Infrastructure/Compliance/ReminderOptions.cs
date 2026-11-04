namespace CMS.Infrastructure.Compliance;

public class ReminderOptions
{
    public const string SectionName = "Reminders";

    public bool Enabled { get; set; } = true;

    /// <summary>Days ahead of today an obligation must be due within to appear in the UpcomingDue digest.</summary>
    public int WindowDays { get; set; } = 7;

    /// <summary>UTC hour-of-day (0..23) the background worker fires its daily sweep.</summary>
    public int DailyRunHourUtc { get; set; } = 4;
}
