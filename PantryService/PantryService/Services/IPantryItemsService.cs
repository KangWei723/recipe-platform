using PantryService.Dtos;

namespace PantryService.Services;

public interface IPantryItemsService
{
    Task<List<PantryItemResponse>> GetForUserAsync(long userId);
    Task<PantryItemResponse> UpsertAsync(long userId, long ingredientId);
    Task DeleteAsync(long userId, long ingredientId);
    Task<MissingIngredientsResponse> GetMissingIngredientsAsync(long userId, long recipeId);
}
