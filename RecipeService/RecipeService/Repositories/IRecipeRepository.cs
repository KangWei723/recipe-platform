using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(long id);
    Task<List<Recipe>> GetAllAsync();
    Task<Recipe> AddAsync(Recipe recipe);
}
