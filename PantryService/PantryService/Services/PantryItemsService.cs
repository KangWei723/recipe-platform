using Messaging;
using PantryService.Client;
using PantryService.Domain;
using PantryService.Dtos;
using PantryService.Exceptions;
using PantryService.Repositories;

namespace PantryService.Services;

public class PantryItemsService(
    IPantryItemRepository repository,
    IRecipeServiceClient recipeServiceClient,
    IQStashPublisher qstashPublisher,
    ILogger<PantryItemsService> logger) : IPantryItemsService
{
    public async Task<List<PantryItemResponse>> GetForUserAsync(long userId)
    {
        var items = await repository.GetForUserAsync(userId);
        var ingredientNames = await FetchIngredientNamesAsync(items.Select(i => i.IngredientId));
        return items.Select(i => ToResponse(i, ingredientNames)).ToList();
    }

    public async Task<PantryItemResponse> UpsertAsync(long userId, long ingredientId)
    {
        var ingredient = await recipeServiceClient.GetIngredientAsync(ingredientId)
            ?? throw new ValidationException($"Ingredient {ingredientId} does not exist");

        var item = await repository.UpsertAsync(userId, ingredientId);

        return ToResponse(item, ingredient.Name);
    }

    public async Task DeleteAsync(long userId, long ingredientId)
    {
        var deleted = await repository.DeleteAsync(userId, ingredientId);
        if (!deleted)
        {
            throw new NotFoundException($"Ingredient {ingredientId} not found in pantry for user {userId}");
        }
    }

    public async Task<MissingIngredientsResponse> GetMissingIngredientsAsync(long userId, long recipeId)
    {
        var recipe = await recipeServiceClient.GetRecipeAsync(recipeId)
            ?? throw new NotFoundException($"Recipe {recipeId} not found");

        var pantryItems = await repository.GetForUserAsync(userId);
        var pantryIngredientIds = pantryItems.Select(p => p.IngredientId).ToHashSet();

        // Presence-only: an ingredient is missing if it's simply not in the pantry at all --
        // there's no "have some but not enough" case anymore since pantry doesn't track quantity.
        var missing = recipe.Ingredients
            .Where(ri => !ri.Optional)
            .Where(ri => !pantryIngredientIds.Contains(ri.IngredientId))
            .Select(ri => new MissingIngredientResponse(ri.IngredientId, ri.IngredientName))
            .ToList();

        await PublishMissingIngredientEventsAsync(userId, recipe.Id, missing);

        return new MissingIngredientsResponse(recipe.Id, recipe.Title, missing);
    }

    // The decoupled event side-channel: the Sourcing consumer acts on this asynchronously. A
    // QStash outage must not break the synchronous response above, so publish failures are
    // logged and swallowed rather than propagated.
    private async Task PublishMissingIngredientEventsAsync(
        long userId, long recipeId, List<MissingIngredientResponse> missing)
    {
        foreach (var item in missing)
        {
            try
            {
                await qstashPublisher.PublishAsync(QStashTopics.IngredientMissing, new IngredientMissingEvent(
                    userId,
                    recipeId,
                    item.IngredientId,
                    item.IngredientName,
                    DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to publish ingredient.missing event for ingredient {IngredientId} (user {UserId}, recipe {RecipeId})",
                    item.IngredientId, userId, recipeId);
            }
        }
    }

    private async Task<Dictionary<long, string>> FetchIngredientNamesAsync(IEnumerable<long> ingredientIds)
    {
        var distinctIds = ingredientIds.Distinct().ToList();
        var lookups = await Task.WhenAll(distinctIds.Select(async id =>
        {
            var ingredient = await recipeServiceClient.GetIngredientAsync(id);
            return (id, name: ingredient?.Name ?? "Unknown ingredient");
        }));

        return lookups.ToDictionary(l => l.id, l => l.name);
    }

    private static PantryItemResponse ToResponse(PantryItem item, Dictionary<long, string> ingredientNames) =>
        ToResponse(item, ingredientNames.GetValueOrDefault(item.IngredientId, "Unknown ingredient"));

    private static PantryItemResponse ToResponse(PantryItem item, string ingredientName) =>
        new(item.IngredientId, ingredientName, item.UpdatedAt);
}
