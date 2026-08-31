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

    // General-locator providers (Google Places today) ignore their ingredientName argument by
    // contract -- see IStoreProvider.IsIngredientSpecific -- so there's no real value to pass
    // here. Also used for the Mock fallback in FindGeneralAsync, which has no ingredient to seed
    // its placeholder price from in this flow either.
    private const string NoIngredient = "";

    public async Task<NearbySourcingResponse> FindConfirmedAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var scope = $"confirmed:{ingredientName.Trim().ToLowerInvariant()}";
        var cached = await cache.GetAsync(scope, lat, lng, cancellationToken);
        if (cached is not null)
        {
            var cacheDiagnostic = new ProviderDiagnostic("Cache", ProviderOutcome.Succeeded, 0, null);
            return new NearbySourcingResponse(ingredientName, lat, lng, cached, [cacheDiagnostic]);
        }

        var confirmedProviders = providers.Where(p => p.IsIngredientSpecific);
        var (offers, diagnostics) = await RunProvidersAsync(confirmedProviders, ingredientName, lat, lng, cancellationToken);

        // No Mock fallback here, deliberately: this flow's whole point is "found" vs. "not
        // found," not "here's a placeholder guess" -- an empty result stays empty.
        if (offers.Count > 0)
        {
            await cache.SetAsync(scope, lat, lng, offers, cancellationToken);
        }

        return new NearbySourcingResponse(ingredientName, lat, lng, offers, diagnostics);
    }

    public async Task<NearbyGeneralSourcingResponse> FindGeneralAsync(
        double lat, double lng, CancellationToken cancellationToken)
    {
        const string scope = "general";
        var cached = await cache.GetAsync(scope, lat, lng, cancellationToken);
        if (cached is not null)
        {
            var cacheDiagnostic = new ProviderDiagnostic("Cache", ProviderOutcome.Succeeded, 0, null);
            return new NearbyGeneralSourcingResponse(lat, lng, cached, [cacheDiagnostic]);
        }

        var generalProviders = providers.Where(p => !p.IsIngredientSpecific);
        var (offers, diagnostics) = await RunProvidersAsync(generalProviders, NoIngredient, lat, lng, cancellationToken);
        var usedMockFallback = false;

        if (offers.Count == 0)
        {
            var mockCall = await CallProviderAsync(mockProvider, NoIngredient, lat, lng, cancellationToken);
            offers = mockCall.Offers.ToList();
            diagnostics = [.. diagnostics, mockCall.Diagnostic];
            usedMockFallback = true;
        }

        // Same "don't cache empty or simulated results" rule as the confirmed flow -- a
        // transient failure shouldn't be pinned in place for the full TTL.
        if (offers.Count > 0 && !usedMockFallback)
        {
            await cache.SetAsync(scope, lat, lng, offers, cancellationToken);
        }

        return new NearbyGeneralSourcingResponse(lat, lng, offers, diagnostics);
    }

    // Races the given providers against their own timeout in parallel (a single slow/failed
    // provider never blocks the others -- see CallProviderAsync), then sorts/caps here so every
    // caller gets the closest MaxResults stores using the same origin coordinate that was
    // searched from.
    private async Task<(IReadOnlyList<StoreOffer> Offers, IReadOnlyList<ProviderDiagnostic> Diagnostics)> RunProvidersAsync(
        IEnumerable<IStoreProvider> selectedProviders, string ingredientName, double lat, double lng,
        CancellationToken cancellationToken)
    {
        var calls = await Task.WhenAll(selectedProviders.Select(
            provider => CallProviderAsync(provider, ingredientName, lat, lng, cancellationToken)));

        var offers = calls.SelectMany(call => call.Offers)
            .OrderBy(offer => DistanceMiles(lat, lng, offer.Lat, offer.Lng) ?? double.MaxValue)
            .Take(MaxResults)
            .ToList();
        var diagnostics = calls.Select(call => call.Diagnostic).ToList();

        return (offers, diagnostics);
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
