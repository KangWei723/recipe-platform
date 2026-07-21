using Microsoft.EntityFrameworkCore;
using RecipeService.Data;
using RecipeService.Domain;

namespace RecipeService.Repositories;

public class RecipeRepository(RecipeDbContext context) : IRecipeRepository
{
    public Task<Recipe?> GetByIdAsync(long id) =>
        context.Recipes
            .Include(r => r.Steps.OrderBy(s => s.StepNumber))
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<List<Recipe>> GetAllAsync() =>
        context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task<Recipe> AddAsync(Recipe recipe)
    {
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
        return recipe;
    }
}
