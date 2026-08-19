using System.Diagnostics;
using SourcingService.Caching;
using SourcingService.Dtos;
using SourcingService.Providers;
using SourcingService.Providers.Mock;

namespace SourcingService.Services;

public class SourcingAggregatorService(
    IEnumerable<IStoreProvider> providers,
    MockStoreProvider mockProvider,
    ISourcingCache cache,
    ILogger<SourcingAggregatorService> logger) : ISourcingAggregatorService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(3);
    private const int MaxResults = 8;
    private const double EarthRadiusMiles = 3958.8;

    public async Task<NearbySourcingResponse> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(ingredientName, lat, lng, cancellationToken);
        if (cached is not null)
        {
            // Cache hit: return immediately without racing the real providers at all.
            var cacheDiagnostic = new ProviderDiagnostic("Cache", ProviderOutcome.Succeeded, 0, null);
            return new NearbySourcingResponse(ingredientName, lat, lng, cached, [cacheDiagnostic]);
        }

        // Every real provider is raced against its own timeout in parallel, so
        // Task.WhenAll returns as soon as the slowest one finishes or hits the
        // 3s cap -- a single slow/failed provider never blocks the others.
        var calls = await Task.WhenAll(providers.Select(
            provider => CallProviderAsync(provider, ingredientName, lat, lng, cancellationToken)));

        // Sorted/capped here (rather than left to the client) so every caller --
        // the standalone lookup and the nested per-ingredient resolver alike --
        // gets the closest MaxResults stores using the same origin coordinate
        // that was searched from, and so cached entries store the final list.
        var offers = calls.SelectMany(call => call.Offers)
            .OrderBy(offer => DistanceMiles(lat, lng, offer.Lat, offer.Lng) ?? double.MaxValue)
            .Take(MaxResults)
            .ToList();
        var diagnostics = calls.Select(call => call.Diagnostic).ToList();
        var usedMockFallback = false;

        if (offers.Count == 0)
        {
            var mockCall = await CallProviderAsync(mockProvider, ingredientName, lat, lng, cancellationToken);
            offers = mockCall.Offers.ToList();
            diagnostics.Add(mockCall.Diagnostic);
            usedMockFallback = true;
        }

        // Don't cache empty or simulated results -- a transient provider failure
        // shouldn't be pinned in place for the full TTL when a retry soon after
        // might reach the real providers successfully.
        if (offers.Count > 0 && !usedMockFallback)
        {
            await cache.SetAsync(ingredientName, lat, lng, offers, cancellationToken);
        }

        return new NearbySourcingResponse(ingredientName, lat, lng, offers, diagnostics);
    }

    private async Task<(IReadOnlyList<StoreOffer> Offers, ProviderDiagnostic Diagnostic)> CallProviderAsync(
        IStoreProvider provider, string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(ProviderTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var offers = await provider.FindNearbyAsync(ingredientName, lat, lng, linkedCts.Token);
            stopwatch.Stop();

            logger.LogInformation(
                "Sourcing provider {Provider} outcome={Outcome} elapsedMs={ElapsedMs} offerCount={OfferCount}",
                provider.Name, ProviderOutcome.Succeeded, stopwatch.ElapsedMilliseconds, offers.Count);

            return (offers, new ProviderDiagnostic(provider.Name, ProviderOutcome.Succeeded, stopwatch.ElapsedMilliseconds, null));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogWarning(
                "Sourcing provider {Provider} outcome={Outcome} elapsedMs={ElapsedMs} timeoutMs={TimeoutMs}",
                provider.Name, ProviderOutcome.TimedOut, stopwatch.ElapsedMilliseconds, ProviderTimeout.TotalMilliseconds);

            return (
                [],
                new ProviderDiagnostic(
                    provider.Name, ProviderOutcome.TimedOut, stopwatch.ElapsedMilliseconds,
                    $"Exceeded {ProviderTimeout.TotalSeconds}s timeout"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogWarning(
                ex, "Sourcing provider {Provider} outcome={Outcome} elapsedMs={ElapsedMs}",
                provider.Name, ProviderOutcome.Failed, stopwatch.ElapsedMilliseconds);

            return (
                [],
                new ProviderDiagnostic(provider.Name, ProviderOutcome.Failed, stopwatch.ElapsedMilliseconds, ex.Message));
        }
    }

    private static double? DistanceMiles(double originLat, double originLng, double? lat, double? lng)
    {
        if (lat is null || lng is null)
        {
            return null;
        }

        var dLat = ToRadians(lat.Value - originLat);
        var dLng = ToRadians(lng.Value - originLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(originLat)) * Math.Cos(ToRadians(lat.Value)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
