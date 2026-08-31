using SourcingService.Dtos;

namespace SourcingService.Caching;

public interface ISourcingCache
{
    // "scope" identifies what was searched for -- e.g. "confirmed:{ingredientName}" for the
    // per-ingredient flow, or the constant "general" for the ingredient-agnostic locator flow --
    // rather than always being an ingredient name, since the general flow has no ingredient
    // dimension. Callers (SourcingAggregatorService) build the scope string.
    Task<IReadOnlyList<StoreOffer>?> GetAsync(
        string scope, double lat, double lng, CancellationToken cancellationToken);

    Task SetAsync(
        string scope, double lat, double lng, IReadOnlyList<StoreOffer> offers,
        CancellationToken cancellationToken);
}
