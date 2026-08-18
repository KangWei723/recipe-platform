using Microsoft.EntityFrameworkCore;
using RecipeService.Data;
using RecipeService.Domain;

namespace RecipeService.Repositories;

public class IngredientRepository(RecipeDbContext context) : IIngredientRepository
{
    public Task<Ingredient?> GetByIdAsync(long id) =>
        context.Ingredients.FirstOrDefaultAsync(i => i.Id == id);

    public Task<List<Ingredient>> GetAllAsync() =>
        context.Ingredients.OrderBy(i => i.Name).ToListAsync();

    public Task<List<Ingredient>> GetByIdsAsync(IEnumerable<long> ids) =>
        context.Ingredients.Where(i => ids.Contains(i.Id)).ToListAsync();

    public async Task<Ingredient> AddAsync(Ingredient ingredient)
    {
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<Ingredient?> UpdateAsync(long id, Action<Ingredient> apply)
    {
        var ingredient = await context.Ingredients.FirstOrDefaultAsync(i => i.Id == id);
        if (ingredient is null)
        {
            return null;
        }

        apply(ingredient);
        await context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var ingredient = await context.Ingredients.FirstOrDefaultAsync(i => i.Id == id);
        if (ingredient is null)
        {
            return false;
        }

        context.Ingredients.Remove(ingredient);
        await context.SaveChangesAsync();
        return true;
    }

    // Backs the friendly "used in N recipe(s)" conflict message on delete -- recipe_ingredients
    // already has a real DB foreign key to ingredients with no ON DELETE CASCADE, so Postgres
    // would reject the delete anyway; this pre-check just replaces that raw constraint-violation
    // error with a readable one.
    public Task<int> CountRecipeUsagesAsync(long ingredientId) =>
        context.RecipeIngredients.CountAsync(ri => ri.IngredientId == ingredientId);
}
