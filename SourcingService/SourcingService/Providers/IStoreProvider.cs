using SourcingService.Dtos;

namespace SourcingService.Providers;

public interface IStoreProvider
{
    string Name { get; }

    Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken);
}
