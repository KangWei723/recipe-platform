using System.Diagnostics;
using SourcingService.Dtos;
using SourcingService.Providers;
using SourcingService.Providers.Mock;

namespace SourcingService.Services;

public class SourcingAggregatorService(
    IEnumerable<IStoreProvider> providers,
    MockStoreProvider mockProvider,
    ILogger<SourcingAggregatorService> logger) : ISourcingAggregatorService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(3);

    public async Task<NearbySourcingResponse> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        // Every real provider is raced against its own timeout in parallel, so
        // Task.WhenAll returns as soon as the slowest one finishes or hits the
        // 3s cap -- a single slow/failed provider never blocks the others.
        var calls = await Task.WhenAll(providers.Select(
            provider => CallProviderAsync(provider, ingredientName, lat, lng, cancellationToken)));

        var offers = calls.SelectMany(call => call.Offers).ToList();
        var diagnostics = calls.Select(call => call.Diagnostic).ToList();

        if (offers.Count == 0)
        {
            var mockCall = await CallProviderAsync(mockProvider, ingredientName, lat, lng, cancellationToken);
            offers = mockCall.Offers.ToList();
            diagnostics.Add(mockCall.Diagnostic);
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
}
