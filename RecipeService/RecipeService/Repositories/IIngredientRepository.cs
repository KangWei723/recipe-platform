using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface IIngredientRepository
{
    Task<Ingredient?> GetByIdAsync(long id);
    Task<List<Ingredient>> GetAllAsync();
    Task<List<Ingredient>> GetByIdsAsync(IEnumerable<long> ids);
    Task<Ingredient> AddAsync(Ingredient ingredient);
    Task<Ingredient?> UpdateAsync(long id, Action<Ingredient> apply);
    Task<bool> DeleteAsync(long id);
    Task<int> CountRecipeUsagesAsync(long ingredientId);
}
