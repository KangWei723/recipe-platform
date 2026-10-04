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

    // Full replace of steps/ingredients (PUT semantics, matching CreateRecipeRequest). Clearing
    // and saving before adding the new rows -- rather than clearing and adding within one
    // SaveChangesAsync -- is deliberate: EF Core doesn't guarantee deletes are sent before
    // inserts within a single call, and recipe_steps has a UNIQUE(recipe_id, step_number)
    // constraint, so a same-transaction clear+add of e.g. step_number 1 could race its own
    // delete and violate the constraint. Two saves make the ordering explicit instead of relying
    // on EF Core's internal command batching.
    public async Task<Recipe?> UpdateAsync(
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
        string? pairing)
    {
        var recipe = await context.Recipes
            .Include(r => r.Steps)
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (recipe is null)
        {
            return null;
        }

        recipe.Title = title;
        recipe.Description = description;
        recipe.Servings = servings;
        recipe.PrepTimeMin = prepTimeMin;
        recipe.CookTimeMin = cookTimeMin;
        recipe.ImageUrl = imageUrl;
        recipe.Tips = tips;
        recipe.Pairing = pairing;
        recipe.Steps.Clear();
        recipe.Ingredients.Clear();
        await context.SaveChangesAsync();

        recipe.Steps.AddRange(steps);
        recipe.Ingredients.AddRange(ingredients);
        await context.SaveChangesAsync();

        return recipe;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var recipe = await context.Recipes.FirstOrDefaultAsync(r => r.Id == id);
        if (recipe is null)
        {
            return false;
        }

        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync();
        return true;
    }
}
