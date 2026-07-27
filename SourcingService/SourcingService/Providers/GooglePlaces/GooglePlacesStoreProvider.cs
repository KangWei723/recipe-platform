using SourcingService.Dtos;

namespace SourcingService.Providers.GooglePlaces;

// Google Places has no product-level pricing, so unlike Kroger it contributes
// general "nearby stores" results independent of the requested ingredient.
public class GooglePlacesStoreProvider(IGooglePlacesClient client) : IStoreProvider
{
    public string Name => "GooglePlaces";

    public async Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var places = await client.FindNearbyGroceryStoresAsync(lat, lng, cancellationToken);

        return places.Select(place => new StoreOffer(
            ProviderName: "GooglePlaces",
            StoreName: place.Name ?? "Unknown store",
            Address: place.Vicinity,
            Lat: place.Geometry?.Location?.Lat,
            Lng: place.Geometry?.Location?.Lng,
            Price: null,
            Currency: null,
            IsSimulated: false)).ToList();
    }
}
