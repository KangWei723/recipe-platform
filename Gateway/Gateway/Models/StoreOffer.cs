namespace Gateway.Models;

public class StoreOffer
{
    public required string ProviderName { get; init; }
    public required string StoreName { get; init; }
    public string? Address { get; init; }
    public double? Lat { get; init; }
    public double? Lng { get; init; }
    public decimal? Price { get; init; }
    public string? Currency { get; init; }
    public required bool IsSimulated { get; init; }
    public string? PlaceId { get; init; }
}
