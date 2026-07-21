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

    public async Task<PantryItem> UpsertAsync(
        long userId, long ingredientId, decimal quantity, string unit, DateOnly? expiryDate)
    {
        var existing = await GetAsync(userId, ingredientId);
        if (existing is not null)
        {
            existing.Quantity = quantity;
            existing.Unit = unit;
            existing.ExpiryDate = expiryDate;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            existing = new PantryItem
            {
                UserId = userId,
                IngredientId = ingredientId,
                Quantity = quantity,
                Unit = unit,
                ExpiryDate = expiryDate,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.PantryItems.Add(existing);
        }

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(long userId, long itemId)
    {
        var item = await context.PantryItems
            .FirstOrDefaultAsync(p => p.Id == itemId && p.UserId == userId);
        if (item is null)
        {
            return false;
        }

        context.PantryItems.Remove(item);
        await context.SaveChangesAsync();
        return true;
    }
}
