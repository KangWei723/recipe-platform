using Microsoft.AspNetCore.Mvc;
using PantryService.Dtos;
using PantryService.Services;

namespace PantryService.Controllers;

[ApiController]
[Route("api/pantry/users/{userId:long}")]
public class PantryController(IPantryItemsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PantryItemResponse>>> GetForUser(long userId) =>
        Ok(await service.GetForUserAsync(userId));

    [HttpPut("items")]
    public async Task<ActionResult<PantryItemResponse>> Upsert(
        long userId, [FromBody] UpsertPantryItemRequest request) =>
        Ok(await service.UpsertAsync(userId, request));

    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> Delete(long userId, long itemId)
    {
        await service.DeleteAsync(userId, itemId);
        return NoContent();
    }

    [HttpGet("recipes/{recipeId:long}/missing-ingredients")]
    public async Task<ActionResult<MissingIngredientsResponse>> GetMissingIngredients(long userId, long recipeId) =>
        Ok(await service.GetMissingIngredientsAsync(userId, recipeId));
}
