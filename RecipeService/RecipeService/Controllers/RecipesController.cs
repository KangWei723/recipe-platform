using Auth;
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

    // POST rather than GET+query-string: an ingredient set can be large enough that it
    // doesn't comfortably fit a query string, and this is a computation over the caller's
    // input rather than a lookup by id.
    [HttpPost("match")]
    public async Task<ActionResult<List<RecipeMatchResponse>>> Match([FromBody] RecipeMatchRequest request) =>
        Ok(await service.GetMatchesAsync(request.IngredientIds));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<RecipeDetailResponse>> Create([FromBody] CreateRecipeRequest request)
    {
        // AuthorId used to be a client-supplied field on the request body -- any caller could
        // publish a recipe under someone else's name by just changing it. Derive it from the
        // caller's own validated token instead.
        var author = await usersService.ResolveCurrentUserAsync(User);
        var created = await service.CreateAsync(request, author.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<RecipeDetailResponse>> Update(long id, [FromBody] UpdateRecipeRequest request) =>
        Ok(await service.UpdateAsync(id, request));

    [HttpDelete("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }
}
