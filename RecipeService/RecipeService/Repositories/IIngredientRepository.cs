using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface IIngredientRepository
{
    Task<Ingredient?> GetByIdAsync(long id);
    Task<List<Ingredient>> GetAllAsync();
    Task<List<Ingredient>> GetByIdsAsync(IEnumerable<long> ids);
    Task<Ingredient> AddAsync(Ingredient ingredient);
}
