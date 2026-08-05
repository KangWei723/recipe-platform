using System.Security.Claims;
using PantryService.Client;

namespace PantryService.Auth;

public class CurrentUserResolver(IRecipeServiceClient recipeServiceClient, ICurrentUserCache cache)
    : ICurrentUserResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<long> ResolveUserIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sub = principal.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("Validated token is missing a 'sub' claim");

        if (cache.TryGet(sub, out var cachedUserId))
        {
            return cachedUserId;
        }

        var user = await recipeServiceClient.ResolveCurrentUserAsync(cancellationToken);
        cache.Set(sub, user.Id, CacheTtl);
        return user.Id;
    }
}
