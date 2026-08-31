namespace Gateway.Client;

// Minimal client-side mirrors of sourcing-service's JSON response contracts.
// See RecipeApiModels.cs for why these are duplicated rather than shared.

public record StoreOfferDto(
    string ProviderName,
    string StoreName,
    string? Address,
    double? Lat,
    double? Lng,
    decimal? Price,
    string? Currency,
    bool IsSimulated,
    string? PlaceId,
    string? StoreId,
    string? ProductId,
    string? ProductName
);

public record NearbySourcingDto(
    string IngredientName,
    double Lat,
    double Lng,
    IReadOnlyList<StoreOfferDto> Results
);

// General, ingredient-agnostic lookup response -- no IngredientName, unlike NearbySourcingDto.
public record NearbyGeneralSourcingDto(
    double Lat,
    double Lng,
    IReadOnlyList<StoreOfferDto> Results
);
