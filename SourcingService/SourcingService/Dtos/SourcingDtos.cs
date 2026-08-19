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
    string? PlaceId = null
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

public static class ProviderOutcome
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";
}
