namespace Gateway.Client;

public interface ISourcingServiceClient
{
    Task<NearbySourcingDto> GetNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken = default);
}
