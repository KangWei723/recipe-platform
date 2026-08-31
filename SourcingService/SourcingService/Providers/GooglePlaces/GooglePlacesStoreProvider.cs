using SourcingService.Dtos;

namespace SourcingService.Providers.GooglePlaces;

// Google Places has no product-level pricing, so unlike Kroger it contributes
// general "nearby stores" results independent of the requested ingredient.
public class GooglePlacesStoreProvider(IGooglePlacesClient client) : IStoreProvider
{
    public string Name => "GooglePlaces";
    public bool IsIngredientSpecific => false;

    // The Nearby Search API only accepts one "type" value per request, so the
    // request-level filter (grocery_or_supermarket) can't also exclude types --
    // Google sometimes tags a gas station's attached mart with grocery_or_supermarket
    // too, which is why a BP could otherwise show up here. Drop anything that's
    // also typed as a gas station or convenience store to filter those out.
    private static readonly HashSet<string> ExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gas_station",
        "convenience_store",
    };

    public async Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var places = await client.FindNearbyGroceryStoresAsync(lat, lng, cancellationToken);

        return places
            .Where(place => place.Types is null || !place.Types.Any(ExcludedTypes.Contains))
            .Select(place => new StoreOffer(
                ProviderName: "GooglePlaces",
                StoreName: place.Name ?? "Unknown store",
                Address: place.Vicinity,
                Lat: place.Geometry?.Location?.Lat,
                Lng: place.Geometry?.Location?.Lng,
                Price: null,
                Currency: null,
                IsSimulated: false,
                PlaceId: place.PlaceId))
            .ToList();
    }
}
