using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IRecipesService
{
    Task<RecipeDetailResponse> GetByIdAsync(long id);
    Task<List<RecipeSummaryResponse>> GetAllAsync();
    Task<RecipeDetailResponse> CreateAsync(CreateRecipeRequest request, long authorId);
    Task<RecipeDetailResponse> UpdateAsync(long id, UpdateRecipeRequest request);
    Task DeleteAsync(long id);
    Task<List<RecipeMatchResponse>> GetMatchesAsync(List<long> ingredientIds);
}
