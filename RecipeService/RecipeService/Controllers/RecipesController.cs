using Microsoft.AspNetCore.Mvc;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController(IRecipesService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RecipeSummaryResponse>>> GetAll() =>
        Ok(await service.GetAllAsync());

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RecipeDetailResponse>> GetById(long id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<ActionResult<RecipeDetailResponse>> Create([FromBody] CreateRecipeRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
