using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeService.Domain;
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

    // The catalog default-unit dropdowns (Manage Ingredients, Add Recipe) are populated from
    // here rather than each client hardcoding its own copy of the unit list -- see
    // RecipeService.Domain.MeasurementUnits.
    [HttpGet("units")]
    public ActionResult<List<MeasurementUnitResponse>> GetUnits() =>
        Ok(MeasurementUnits.All
            .Select(u => new MeasurementUnitResponse(u.Code, u.Label, u.Style == QuantityInputStyle.FractionalFriendly))
            .ToList());

    // Same reasoning as GetUnits: the Manage Ingredients category dropdown is populated from
    // here rather than hardcoding its own copy -- see RecipeService.Domain.IngredientCategories.
    [HttpGet("categories")]
    public ActionResult<List<IngredientCategoryResponse>> GetCategories() =>
        Ok(IngredientCategories.All
            .Select(c => new IngredientCategoryResponse(c.Code, c.Label))
            .ToList());

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

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IngredientResponse>> Update(long id, [FromBody] UpdateIngredientRequest request) =>
        Ok(await service.UpdateAsync(id, request));

    [HttpDelete("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }
}
