using SourcingService.Dtos;

namespace SourcingService.Providers.Mock;

// Fallback used only when neither real provider returns results for this
// ingredient/region combination (e.g. the certification-environment store
// has no matching product, or Google Places found nothing nearby). Results
// are flagged via IsSimulated so callers never mistake them for real pricing.
public class MockStoreProvider : IStoreProvider
{
    public string Name => "Mock";

    // Never actually consulted for routing -- Mock isn't in the DI-registered IStoreProvider
    // collection SourcingAggregatorService filters (see Program.cs), it's injected and invoked
    // separately as the general-flow-only fallback. Still required to satisfy the interface.
    public bool IsIngredientSpecific => false;

    public Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var random = new Random(ingredientName.ToLowerInvariant().GetHashCode());
        var price = Math.Round((decimal)(random.NextDouble() * 8 + 1.5), 2);

        IReadOnlyList<StoreOffer> offers =
        [
            new StoreOffer(
                ProviderName: "Mock",
                StoreName: "Simulated Corner Market",
                Address: "Simulated data -- no real store/price available for this ingredient or region",
                Lat: lat,
                Lng: lng,
                Price: price,
                Currency: "USD",
                IsSimulated: true,
                PlaceId: null)
        ];

        return Task.FromResult(offers);
    }
}
