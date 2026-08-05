using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SourcingService.Caching;

public static class SourcingCacheInstrumentation
{
    public const string ActivitySourceName = "RecipePlatform.SourcingCache";
    public const string MeterName = "RecipePlatform.SourcingCache";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> LookupCounter = Meter.CreateCounter<long>(
        "sourcing.cache.lookups",
        unit: "{lookup}",
        description: "Sourcing cache lookups, tagged by cache.result=hit|miss.");
}
