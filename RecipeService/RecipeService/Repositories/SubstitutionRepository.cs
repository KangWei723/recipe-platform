using Microsoft.EntityFrameworkCore;
using RecipeService.Data;
using RecipeService.Domain;

namespace RecipeService.Repositories;

public class SubstitutionRepository(RecipeDbContext context) : ISubstitutionRepository
{
    public Task<List<IngredientSubstitution>> GetForIngredientAsync(long ingredientId) =>
        context.IngredientSubstitutions
            .Include(s => s.Substitute)
            .Where(s => s.IngredientId == ingredientId)
            .OrderByDescending(s => s.Ratio)
            .ToListAsync();

    public async Task<IngredientSubstitution> AddAsync(IngredientSubstitution substitution)
    {
        context.IngredientSubstitutions.Add(substitution);
        await context.SaveChangesAsync();
        return substitution;
    }
}
