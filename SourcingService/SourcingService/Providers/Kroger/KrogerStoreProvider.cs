using SourcingService.Dtos;

namespace SourcingService.Providers.Kroger;

public class KrogerStoreProvider(IKrogerClient krogerClient) : IStoreProvider
{
    public string Name => "Kroger";
    public bool IsIngredientSpecific => true;

    public async Task<IReadOnlyList<StoreOffer>> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken)
    {
        var location = await krogerClient.FindNearestLocationAsync(lat, lng, cancellationToken);
        if (location is null)
        {
            return [];
        }

        var products = await krogerClient.SearchProductsAsync(ingredientName, location.LocationId, cancellationToken);
        return products.Select(product => ToOffer(product, location)).ToList();
    }

    private static StoreOffer ToOffer(KrogerProduct product, KrogerLocation location)
    {
        var price = product.Items?.FirstOrDefault()?.Price?.Regular;

        return new StoreOffer(
            ProviderName: "Kroger",
            StoreName: location.Name ?? $"Kroger #{location.LocationId}",
            Address: FormatAddress(location.Address),
            Lat: location.Geolocation?.Latitude,
            Lng: location.Geolocation?.Longitude,
            Price: price,
            Currency: price is null ? null : "USD",
            IsSimulated: false,
            PlaceId: null,
            StoreId: location.LocationId,
            ProductId: product.ProductId,
            // Description is nullable in Kroger's response; fall back to something identifiable
            // rather than surfacing a blank product name in a grouped list.
            ProductName: product.Description ?? $"Item {product.ProductId}");
    }

    private static string? FormatAddress(KrogerAddress? address) =>
        address is null ? null : $"{address.AddressLine1}, {address.City}, {address.State} {address.ZipCode}";
}
