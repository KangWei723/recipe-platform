using Microsoft.EntityFrameworkCore;
using PantryService.Data;
using PantryService.Domain;

namespace PantryService.Repositories;

public class PantryItemRepository(PantryDbContext context) : IPantryItemRepository
{
    public Task<List<PantryItem>> GetForUserAsync(long userId) =>
        context.PantryItems
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.IngredientId)
            .ToListAsync();

    public Task<PantryItem?> GetAsync(long userId, long ingredientId) =>
        context.PantryItems
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IngredientId == ingredientId);

    public Task<PantryItem?> GetByIdAsync(long id) =>
        context.PantryItems.FirstOrDefaultAsync(p => p.Id == id);

    // Presence-only, so there's nothing to update on a repeat call besides the touch
    // timestamp -- calling this twice for the same ingredient just confirms it's still there.
    public async Task<PantryItem> UpsertAsync(long userId, long ingredientId)
    {
        var existing = await GetAsync(userId, ingredientId);
        if (existing is not null)
        {
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            return existing;
        }

        existing = new PantryItem
        {
            UserId = userId,
            IngredientId = ingredientId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.PantryItems.Add(existing);

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(long userId, long ingredientId)
    {
        var item = await context.PantryItems
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IngredientId == ingredientId);
        if (item is null)
        {
            return false;
        }

        context.PantryItems.Remove(item);
        await context.SaveChangesAsync();
        return true;
    }
}
