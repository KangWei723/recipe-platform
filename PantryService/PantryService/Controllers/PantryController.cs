using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PantryService.Auth;
using PantryService.Dtos;
using PantryService.Services;

namespace PantryService.Controllers;

// userId used to be a client-supplied {userId} route parameter here -- any caller could
// read/write/delete another user's pantry just by changing the URL, since nothing verified
// the caller actually was that user. It's now derived from the caller's own validated token
// via ICurrentUserResolver, so it's no longer client-controlled at all.
[ApiController]
[Authorize]
[Route("api/pantry")]
public class PantryController(IPantryItemsService service, ICurrentUserResolver currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PantryItemResponse>>> GetForUser(CancellationToken cancellationToken)
    {
        var userId = await currentUser.ResolveUserIdAsync(User, cancellationToken);
        return Ok(await service.GetForUserAsync(userId));
    }

    [HttpPut("items")]
    public async Task<ActionResult<PantryItemResponse>> Upsert(
        [FromBody] UpsertPantryItemRequest request, CancellationToken cancellationToken)
    {
        var userId = await currentUser.ResolveUserIdAsync(User, cancellationToken);
        return Ok(await service.UpsertAsync(userId, request));
    }

    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> Delete(long itemId, CancellationToken cancellationToken)
    {
        var userId = await currentUser.ResolveUserIdAsync(User, cancellationToken);
        await service.DeleteAsync(userId, itemId);
        return NoContent();
    }

    [HttpGet("recipes/{recipeId:long}/missing-ingredients")]
    public async Task<ActionResult<MissingIngredientsResponse>> GetMissingIngredients(
        long recipeId, CancellationToken cancellationToken)
    {
        var userId = await currentUser.ResolveUserIdAsync(User, cancellationToken);
        return Ok(await service.GetMissingIngredientsAsync(userId, recipeId));
    }
}
