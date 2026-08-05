using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes")]
public class RecipesController(IRecipesService service, IUsersService usersService) : ControllerBase
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
        // AuthorId used to be a client-supplied field on the request body -- any caller could
        // publish a recipe under someone else's name by just changing it. Derive it from the
        // caller's own validated token instead.
        var author = await usersService.ResolveCurrentUserAsync(User);
        var created = await service.CreateAsync(request, author.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
