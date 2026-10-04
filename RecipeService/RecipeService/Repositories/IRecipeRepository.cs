using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(long id);
    Task<List<Recipe>> GetAllAsync();
    Task<Recipe> AddAsync(Recipe recipe);
    Task<Recipe?> UpdateAsync(
        long id,
        string title,
        string? description,
        int? servings,
        int? prepTimeMin,
        int? cookTimeMin,
        string? imageUrl,
        List<RecipeStep> steps,
        List<RecipeIngredient> ingredients,
        List<string> tips,
        string? pairing);
    Task<bool> DeleteAsync(long id);
}
