using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingService.Dtos;
using SourcingService.Exceptions;
using SourcingService.Services;

namespace SourcingService.Controllers;

[ApiController]
[Authorize]
[Route("api/sourcing")]
public class SourcingController(ISourcingAggregatorService aggregatorService) : ControllerBase
{
    // Confirmed, per-ingredient lookup (e.g. Kroger's real product/price search) -- see
    // ISourcingAggregatorService.FindConfirmedAsync.
    [HttpGet("{ingredientName}/nearby/confirmed")]
    public async Task<ActionResult<NearbySourcingResponse>> GetConfirmedNearby(
        string ingredientName,
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken)
    {
        ValidateCoordinates(lat, lng);
        return Ok(await aggregatorService.FindConfirmedAsync(ingredientName, lat, lng, cancellationToken));
    }

    // General, ingredient-agnostic "nearby stores" lookup (e.g. Google Places) -- see
    // ISourcingAggregatorService.FindGeneralAsync. No ingredientName: this flow isn't scoped to
    // one ingredient, so it's called once per page view rather than once per missing ingredient.
    [HttpGet("nearby/general")]
    public async Task<ActionResult<NearbyGeneralSourcingResponse>> GetGeneralNearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken)
    {
        ValidateCoordinates(lat, lng);
        return Ok(await aggregatorService.FindGeneralAsync(lat, lng, cancellationToken));
    }

    private static void ValidateCoordinates(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw new ValidationException("lat/lng must be valid coordinates");
        }
    }
}
