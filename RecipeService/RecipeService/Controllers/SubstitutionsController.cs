using Microsoft.AspNetCore.Mvc;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Route("api/substitutions")]
public class SubstitutionsController(ISubstitutionsService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SubstitutionResponse>> Create([FromBody] CreateSubstitutionRequest request)
    {
        var created = await service.CreateAsync(request);
        return Ok(created);
    }
}
