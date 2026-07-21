using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface ISubstitutionRepository
{
    Task<List<IngredientSubstitution>> GetForIngredientAsync(long ingredientId);
    Task<IngredientSubstitution> AddAsync(IngredientSubstitution substitution);
}
