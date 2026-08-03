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

    public async Task<PantryItemResponse> UpsertAsync(long userId, UpsertPantryItemRequest request)
    {
        var ingredient = await recipeServiceClient.GetIngredientAsync(request.IngredientId)
            ?? throw new ValidationException($"Ingredient {request.IngredientId} does not exist");

        var item = await repository.UpsertAsync(
            userId, request.IngredientId, request.Quantity, request.Unit, request.ExpiryDate);

        return ToResponse(item, ingredient.Name);
    }

    public async Task DeleteAsync(long userId, long itemId)
    {
        var deleted = await repository.DeleteAsync(userId, itemId);
        if (!deleted)
        {
            throw new NotFoundException($"Pantry item {itemId} not found for user {userId}");
        }
    }

    public async Task<MissingIngredientsResponse> GetMissingIngredientsAsync(long userId, long recipeId)
    {
        var recipe = await recipeServiceClient.GetRecipeAsync(recipeId)
            ?? throw new NotFoundException($"Recipe {recipeId} not found");

        var pantryItems = await repository.GetForUserAsync(userId);
        var pantryByIngredient = pantryItems.ToDictionary(p => p.IngredientId, p => p.Quantity);

        var missing = recipe.Ingredients
            .Where(ri => !ri.Optional)
            .Where(ri => !pantryByIngredient.TryGetValue(ri.IngredientId, out var have) || have < ri.Quantity)
            .Select(ri => new MissingIngredientResponse(
                ri.IngredientId,
                ri.IngredientName,
                ri.Quantity,
                pantryByIngredient.GetValueOrDefault(ri.IngredientId),
                ri.Unit))
            .ToList();

        await PublishMissingIngredientEventsAsync(userId, recipe.Id, missing);

        return new MissingIngredientsResponse(recipe.Id, recipe.Title, missing);
    }

    // The decoupled event side-channel: consumers (Substitution, Sourcing) act on this
    // asynchronously. A QStash outage must not break the synchronous response above, so
    // publish failures are logged and swallowed rather than propagated.
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
                    item.RequiredQuantity,
                    item.AvailableQuantity,
                    item.Unit,
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
        new(
            item.Id,
            item.UserId,
            item.IngredientId,
            ingredientName,
            item.Quantity,
            item.Unit,
            item.ExpiryDate,
            item.UpdatedAt
        );
}
