using System.Text.Json.Serialization;

namespace SourcingService.Providers.Kroger;

public record KrogerTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);

public record KrogerLocationsResponse(
    [property: JsonPropertyName("data")] List<KrogerLocation> Data
);

public record KrogerLocation(
    [property: JsonPropertyName("locationId")] string LocationId,
    [property: JsonPropertyName("chain")] string? Chain,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("address")] KrogerAddress? Address,
    [property: JsonPropertyName("geolocation")] KrogerGeolocation? Geolocation
);

public record KrogerAddress(
    [property: JsonPropertyName("addressLine1")] string? AddressLine1,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("zipCode")] string? ZipCode
);

public record KrogerGeolocation(
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude
);

public record KrogerProductsResponse(
    [property: JsonPropertyName("data")] List<KrogerProduct> Data
);

public record KrogerProduct(
    [property: JsonPropertyName("productId")] string ProductId,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("items")] List<KrogerProductItem>? Items
);

public record KrogerProductItem(
    [property: JsonPropertyName("size")] string? Size,
    [property: JsonPropertyName("price")] KrogerPrice? Price
);

public record KrogerPrice(
    [property: JsonPropertyName("regular")] decimal? Regular,
    [property: JsonPropertyName("promo")] decimal? Promo
);
