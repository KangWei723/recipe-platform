using SourcingService.Dtos;

namespace SourcingService.Caching;

public interface ISourcingCache
{
    Task<IReadOnlyList<StoreOffer>?> GetAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken);

    Task SetAsync(
        string ingredientName, double lat, double lng, IReadOnlyList<StoreOffer> offers,
        CancellationToken cancellationToken);
}
