namespace CMS.Domain.Entities;

public enum ReminderKind
{
    /// <summary>Due within the configured window and still open.</summary>
    UpcomingDue = 1,
    /// <summary>Past due and still open.</summary>
    Overdue = 2,
}
