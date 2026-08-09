using Gateway.Client;
using Gateway.Models;
using HotChocolate;

namespace Gateway.GraphQL;

public class Mutation
{
    // No userId argument, same reason as Query.PantryItemsAsync: pantry-service resolves the
    // caller from the forwarded bearer token, not a client-supplied value.
    public async Task<PantryItem> UpsertPantryItemAsync(
        long ingredientId,
        decimal quantity,
        string unit,
        DateOnly? expiryDate,
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        var dto = await pantryClient.UpsertAsync(
            new UpsertPantryItemDto(ingredientId, quantity, unit, expiryDate),
            cancellationToken);

        return new PantryItem
        {
            Id = dto.Id,
            UserId = dto.UserId,
            IngredientId = dto.IngredientId,
            IngredientName = dto.IngredientName,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            ExpiryDate = dto.ExpiryDate,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public async Task<bool> RemovePantryItemAsync(
        long itemId,
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        await pantryClient.DeleteAsync(itemId, cancellationToken);
        return true;
    }

    // No author argument, same reasoning as the pantry mutations above: recipe-service derives
    // the author from the caller's own forwarded bearer token (see IRecipeServiceClient.CreateAsync),
    // not from anything a client could supply -- this is the same fix already applied to
    // CreateRecipeRequest.AuthorId on the REST side.
    public async Task<RecipeSummary> CreateRecipeAsync(
        string title,
        string? description,
        int? servings,
        int? prepTimeMin,
        int? cookTimeMin,
        List<CreateRecipeStepInput> steps,
        List<CreateRecipeIngredientInput> ingredients,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var dto = await recipeClient.CreateAsync(
            new CreateRecipeDto(
                title,
                description,
                servings,
                prepTimeMin,
                cookTimeMin,
                ImageUrl: null,
                steps
                    .Select(s => new CreateRecipeStepDto(s.StepNumber, s.Instruction, s.TimerSeconds))
                    .ToList(),
                ingredients
                    .Select(i => new CreateRecipeIngredientDto(i.IngredientId, i.Quantity, i.Unit, i.Optional))
                    .ToList()),
            cancellationToken);

        return new RecipeSummary
        {
            Id = dto.Id,
            AuthorId = dto.AuthorId,
            Title = dto.Title,
            Description = dto.Description,
            Servings = dto.Servings,
            PrepTimeMin = dto.PrepTimeMin,
            CookTimeMin = dto.CookTimeMin,
            ImageUrl = dto.ImageUrl,
            CreatedAt = dto.CreatedAt
        };
    }
}
