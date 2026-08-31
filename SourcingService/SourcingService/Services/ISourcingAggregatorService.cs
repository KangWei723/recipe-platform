using SourcingService.Dtos;

namespace SourcingService.Services;

public interface ISourcingAggregatorService
{
    // Confirmed, per-ingredient flow -- only calls providers where IsIngredientSpecific is true
    // (e.g. Kroger's real product/price lookup). Never falls back to simulated data: an empty
    // result here means "not found," not "might have it."
    Task<NearbySourcingResponse> FindConfirmedAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken);

    // General, ingredient-agnostic locator flow -- only calls providers where IsIngredientSpecific
    // is false (e.g. Google Places). Falls back to simulated data on an empty result, same as
    // this flow always has.
    Task<NearbyGeneralSourcingResponse> FindGeneralAsync(
        double lat, double lng, CancellationToken cancellationToken);
}
