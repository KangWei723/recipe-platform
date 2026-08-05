using Gateway.Client;
using Gateway.Models;
using HotChocolate;

namespace Gateway.GraphQL;

public class Query
{
    // Core query: recipe + ingredients + pantry status, resolved together so
    // a single upstream call to pantry-service's missing-ingredients check
    // covers every ingredient instead of one pantry lookup per ingredient.
    // No userId argument: pantry-service resolves "whose pantry" from the caller's own
    // forwarded bearer token, not from a client-supplied value (that used to let any caller
    // read anyone's pantry just by passing a different id here).
    public async Task<Recipe?> GetRecipeAsync(
        long id,
        [Service] IRecipeServiceClient recipeClient,
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        var recipeDto = await recipeClient.GetRecipeAsync(id, cancellationToken);
        if (recipeDto is null)
        {
            return null;
        }

        var missing = await pantryClient.GetMissingIngredientsAsync(id, cancellationToken);
        var missingIngredientIds = missing.MissingIngredients
            .Select(m => m.IngredientId)
            .ToHashSet();

        return new Recipe
        {
            Id = recipeDto.Id,
            AuthorId = recipeDto.AuthorId,
            Title = recipeDto.Title,
            Description = recipeDto.Description,
            Servings = recipeDto.Servings,
            PrepTimeMin = recipeDto.PrepTimeMin,
            CookTimeMin = recipeDto.CookTimeMin,
            ImageUrl = recipeDto.ImageUrl,
            CreatedAt = recipeDto.CreatedAt,
            Steps = recipeDto.Steps
                .Select(s => new RecipeStep
                {
                    Id = s.Id,
                    StepNumber = s.StepNumber,
                    Instruction = s.Instruction,
                    TimerSeconds = s.TimerSeconds
                })
                .ToList(),
            Ingredients = recipeDto.Ingredients
                .Select(i => new RecipeIngredient
                {
                    Id = i.Id,
                    IngredientId = i.IngredientId,
                    IngredientName = i.IngredientName,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Optional = i.Optional,
                    InPantry = !missingIngredientIds.Contains(i.IngredientId)
                })
                .ToList()
        };
    }
}
