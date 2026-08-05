namespace PantryService.Auth;

public interface ICurrentUserCache
{
    bool TryGet(string authSub, out long userId);
    void Set(string authSub, long userId, TimeSpan ttl);
}
