using SourcingService.Dtos;

namespace SourcingService.Providers;

public interface IStoreProvider
{
    string Name { get; }

    // Routes this provider between the two sourcing flows (SourcingAggregatorService): true for
    // a real per-ingredient product/price lookup (e.g. Kroger), false for a general "nearby
    // stores" locator that doesn't actually vary by ingredient (e.g. Google Places).
    bool IsIngredientSpecific { get; }

    Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken);
}
