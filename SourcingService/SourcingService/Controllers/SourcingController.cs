using Microsoft.AspNetCore.Mvc;
using SourcingService.Dtos;
using SourcingService.Exceptions;
using SourcingService.Services;

namespace SourcingService.Controllers;

[ApiController]
[Route("api/sourcing")]
public class SourcingController(ISourcingAggregatorService aggregatorService) : ControllerBase
{
    [HttpGet("{ingredientName}/nearby")]
    public async Task<ActionResult<NearbySourcingResponse>> GetNearby(
        string ingredientName,
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw new ValidationException("lat/lng must be valid coordinates");
        }

        return Ok(await aggregatorService.FindNearbyAsync(ingredientName, lat, lng, cancellationToken));
    }
}
