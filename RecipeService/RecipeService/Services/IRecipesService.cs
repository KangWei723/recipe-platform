using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IRecipesService
{
    Task<RecipeDetailResponse> GetByIdAsync(long id);
    Task<List<RecipeSummaryResponse>> GetAllAsync();
    Task<RecipeDetailResponse> CreateAsync(CreateRecipeRequest request, long authorId);
}
