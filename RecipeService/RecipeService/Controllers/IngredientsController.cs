using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Authorize]
[Route("api/ingredients")]
public class IngredientsController(IIngredientsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IngredientResponse>>> GetAll() =>
        Ok(await service.GetAllAsync());

    [HttpGet("{id:long}")]
    public async Task<ActionResult<IngredientResponse>> GetById(long id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IngredientResponse>> Create([FromBody] CreateIngredientRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
