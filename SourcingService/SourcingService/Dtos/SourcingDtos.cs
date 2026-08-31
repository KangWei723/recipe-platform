namespace SourcingService.Dtos;

public record StoreOffer(
    string ProviderName,
    string StoreName,
    string? Address,
    double? Lat,
    double? Lng,
    decimal? Price,
    string? Currency,
    bool IsSimulated,
    string? PlaceId = null,
    // Populated only by providers whose results are individual products at a store (Kroger),
    // not a general store locator (Google Places) -- lets a client group multiple products
    // under the one store they came from instead of repeating the store per product.
    string? StoreId = null,
    string? ProductId = null,
    string? ProductName = null
);

public record ProviderDiagnostic(
    string ProviderName,
    string Outcome,
    long ElapsedMs,
    string? Error
);

public record NearbySourcingResponse(
    string IngredientName,
    double Lat,
    double Lng,
    IReadOnlyList<StoreOffer> Results,
    IReadOnlyList<ProviderDiagnostic> ProviderDiagnostics
);

// Same shape as NearbySourcingResponse minus IngredientName -- the general-locator flow
// (SourcingAggregatorService.FindGeneralAsync) isn't scoped to any one ingredient.
public record NearbyGeneralSourcingResponse(
    double Lat,
    double Lng,
    IReadOnlyList<StoreOffer> Results,
    IReadOnlyList<ProviderDiagnostic> ProviderDiagnostics
);

public static class ProviderOutcome
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";
}
