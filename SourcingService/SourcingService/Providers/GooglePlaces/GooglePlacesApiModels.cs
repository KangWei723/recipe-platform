using System.Text.Json.Serialization;

namespace SourcingService.Providers.GooglePlaces;

public record GooglePlacesNearbySearchResponse(
    [property: JsonPropertyName("results")] List<GooglePlaceResult>? Results,
    [property: JsonPropertyName("status")] string? Status
);

public record GooglePlaceResult(
    [property: JsonPropertyName("place_id")] string PlaceId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("vicinity")] string? Vicinity,
    [property: JsonPropertyName("geometry")] GooglePlaceGeometry? Geometry
);

public record GooglePlaceGeometry(
    [property: JsonPropertyName("location")] GooglePlaceLocation? Location
);

public record GooglePlaceLocation(
    [property: JsonPropertyName("lat")] double? Lat,
    [property: JsonPropertyName("lng")] double? Lng
);
