using SourcingService.Dtos;

namespace SourcingService.Services;

public interface ISourcingAggregatorService
{
    Task<NearbySourcingResponse> FindNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken);
}
