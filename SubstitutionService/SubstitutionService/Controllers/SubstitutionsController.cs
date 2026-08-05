using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubstitutionService.Dtos;
using SubstitutionService.Services;

namespace SubstitutionService.Controllers;

[ApiController]
[Authorize]
[Route("api/substitutions")]
public class SubstitutionsController(ISubstitutionsService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubstitutionResponse>> Create([FromBody] CreateSubstitutionRequest request)
    {
        var created = await service.CreateAsync(request);
        return Ok(created);
    }

    [HttpGet("{ingredientName}/ranked")]
    public async Task<ActionResult<IReadOnlyList<RankedSubstituteResponse>>> GetRanked(
        string ingredientName,
        [FromQuery] string? context)
    {
        var ranked = await service.GetRankedSubstitutesAsync(ingredientName, context);
        return Ok(ranked);
    }
}
