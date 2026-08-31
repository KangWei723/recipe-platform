namespace Gateway.Client;

public interface ISourcingServiceClient
{
    // Confirmed, per-ingredient lookup (e.g. Kroger's real product/price search).
    Task<NearbySourcingDto> GetConfirmedNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken = default);

    // General, ingredient-agnostic "nearby stores" lookup (e.g. Google Places) -- not scoped to
    // any one ingredient, so no ingredientName parameter.
    Task<NearbyGeneralSourcingDto> GetGeneralNearbyAsync(
        double lat, double lng, CancellationToken cancellationToken = default);
}
