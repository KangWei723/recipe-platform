using PantryService.Dtos;

namespace PantryService.Services;

public interface IPantryItemsService
{
    Task<List<PantryItemResponse>> GetForUserAsync(long userId);
    Task<PantryItemResponse> UpsertAsync(long userId, UpsertPantryItemRequest request);
    Task DeleteAsync(long userId, long itemId);
    Task<MissingIngredientsResponse> GetMissingIngredientsAsync(long userId, long recipeId);
}
