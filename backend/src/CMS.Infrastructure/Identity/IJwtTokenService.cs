using CMS.Domain.Identity;

namespace CMS.Infrastructure.Identity;

public interface IJwtTokenService
{
    Task<AccessToken> IssueAsync(ApplicationUser user, CancellationToken ct = default);
}

public record AccessToken(string Token, DateTimeOffset ExpiresAtUtc, string TokenType = "Bearer");
