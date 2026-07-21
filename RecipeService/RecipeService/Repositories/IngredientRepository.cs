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
}
