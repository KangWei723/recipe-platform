namespace SourcingService.Providers.GooglePlaces;

public interface IGooglePlacesClient
{
    Task<IReadOnlyList<GooglePlaceResult>> FindNearbyGroceryStoresAsync(
        double lat, double lng, CancellationToken cancellationToken);
}
