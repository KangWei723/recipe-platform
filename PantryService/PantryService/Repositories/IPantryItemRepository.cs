using PantryService.Domain;

namespace PantryService.Repositories;

public interface IPantryItemRepository
{
    Task<List<PantryItem>> GetForUserAsync(long userId);
    Task<PantryItem?> GetAsync(long userId, long ingredientId);
    Task<PantryItem?> GetByIdAsync(long id);
    Task<PantryItem> UpsertAsync(long userId, long ingredientId);
    Task<bool> DeleteAsync(long userId, long ingredientId);
}
