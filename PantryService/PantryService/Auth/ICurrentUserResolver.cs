using System.Security.Claims;

namespace PantryService.Auth;

public interface ICurrentUserResolver
{
    /// <summary>
    /// Resolves the caller's numeric recipe-service user id from their validated token's
    /// "sub" claim. This is the trusted replacement for the old client-supplied userId path
    /// parameter -- callers can no longer read/write another user's pantry by changing a URL.
    /// </summary>
    Task<long> ResolveUserIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
