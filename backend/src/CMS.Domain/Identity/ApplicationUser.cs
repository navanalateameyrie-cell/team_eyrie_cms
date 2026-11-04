using Microsoft.AspNetCore.Identity;

namespace CMS.Domain.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>FK to the staff profile this account acts on behalf of. Required for non-system accounts.</summary>
    public Guid? StaffId { get; set; }

    /// <summary>When set, this account is an EXTERNAL client-portal user restricted to one client's data.</summary>
    public Guid? ClientId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
}
