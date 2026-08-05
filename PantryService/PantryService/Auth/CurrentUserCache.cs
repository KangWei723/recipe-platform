using System.Collections.Concurrent;

namespace PantryService.Auth;

// Registered as a singleton so the cache survives across requests (mirrors
// KrogerTokenService's in-memory-cache-with-TTL pattern in SourcingService), while
// CurrentUserResolver itself stays scoped so it can safely depend on the typed,
// transient-by-default IRecipeServiceClient.
public class CurrentUserCache : ICurrentUserCache
{
    private readonly ConcurrentDictionary<string, (long UserId, DateTimeOffset ExpiresAt)> _cache = new();

    public bool TryGet(string authSub, out long userId)
    {
        if (_cache.TryGetValue(authSub, out var entry) && DateTimeOffset.UtcNow < entry.ExpiresAt)
        {
            userId = entry.UserId;
            return true;
        }

        userId = default;
        return false;
    }

    public void Set(string authSub, long userId, TimeSpan ttl) =>
        _cache[authSub] = (userId, DateTimeOffset.UtcNow.Add(ttl));
}
