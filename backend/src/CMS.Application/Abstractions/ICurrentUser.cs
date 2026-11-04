namespace CMS.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? StaffId { get; }
    /// <summary>Set when the signed-in account is an external client-portal user. Internal staff see null.</summary>
    Guid? PortalClientId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);
}
