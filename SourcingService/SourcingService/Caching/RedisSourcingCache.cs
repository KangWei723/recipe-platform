using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SourcingService.Dtos;
using StackExchange.Redis;

namespace SourcingService.Caching;

// Keyed by ingredient name + lat/lng rounded to 2 decimal places (~1.1km at the
// equator) so requests from nearby coordinates -- not just identical ones -- hit
// the same entry, which matters since browser geolocation rarely repeats exactly.
// Redis being unreachable degrades to a cache miss rather than failing the request:
// the aggregator falls back to calling the real providers.
public class RedisSourcingCache(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisSourcingCache> logger) : ISourcingCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<StoreOffer>?> GetAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var key = BuildKey(ingredientName, lat, lng);
        using var activity = SourcingCacheInstrumentation.ActivitySource.StartActivity("sourcing.cache.get");
        activity?.SetTag("sourcing.cache.key", key);

        RedisValue value;
        try
        {
            var db = redis.GetDatabase();
            value = await db.StringGetAsync(key).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Sourcing cache lookup failed for key={Key}, treating as miss", key);
            activity?.SetTag("sourcing.cache.result", "error");
            SourcingCacheInstrumentation.LookupCounter.Add(1, new KeyValuePair<string, object?>("cache.result", "error"));
            return null;
        }

        if (value.IsNullOrEmpty)
        {
            logger.LogInformation(
                "Sourcing cache outcome=miss ingredient={IngredientName} key={Key}", ingredientName, key);
            activity?.SetTag("sourcing.cache.result", "miss");
            SourcingCacheInstrumentation.LookupCounter.Add(1, new KeyValuePair<string, object?>("cache.result", "miss"));
            return null;
        }

        var offers = JsonSerializer.Deserialize<List<StoreOffer>>(value!, SerializerOptions);

        logger.LogInformation(
            "Sourcing cache outcome=hit ingredient={IngredientName} key={Key} offerCount={OfferCount}",
            ingredientName, key, offers?.Count ?? 0);
        activity?.SetTag("sourcing.cache.result", "hit");
        SourcingCacheInstrumentation.LookupCounter.Add(1, new KeyValuePair<string, object?>("cache.result", "hit"));

        return offers;
    }

    public async Task SetAsync(
        string ingredientName, double lat, double lng, IReadOnlyList<StoreOffer> offers,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(ingredientName, lat, lng);
        using var activity = SourcingCacheInstrumentation.ActivitySource.StartActivity("sourcing.cache.set");
        activity?.SetTag("sourcing.cache.key", key);

        try
        {
            var db = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(offers, SerializerOptions);
            var ttl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);
            await db.StringSetAsync(key, payload, ttl).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Sourcing cache write failed for key={Key}", key);
        }
    }

    private static string BuildKey(string ingredientName, double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"sourcing:v1:{ingredientName.Trim().ToLowerInvariant()}:{Math.Round(lat, 2)}:{Math.Round(lng, 2)}");
}
