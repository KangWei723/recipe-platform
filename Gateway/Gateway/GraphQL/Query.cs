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

    public async Task<IReadOnlyList<RecipeSummary>> RecipesAsync(
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var summaries = await recipeClient.GetAllAsync(cancellationToken);
        return summaries
            .Select(r => new RecipeSummary
            {
                Id = r.Id,
                AuthorId = r.AuthorId,
                Title = r.Title,
                Description = r.Description,
                Servings = r.Servings,
                PrepTimeMin = r.PrepTimeMin,
                CookTimeMin = r.CookTimeMin,
                ImageUrl = r.ImageUrl,
                CreatedAt = r.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<Ingredient>> IngredientsAsync(
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var ingredients = await recipeClient.GetIngredientsAsync(cancellationToken);
        return ingredients
            .Select(i => new Ingredient
            {
                Id = i.Id,
                Name = i.Name,
                Category = i.Category,
                DefaultUnit = i.DefaultUnit,
                UsageCount = i.UsageCount
            })
            .ToList();
    }

    // No userId argument, same reason as GetRecipeAsync: pantry-service resolves the caller
    // from the forwarded bearer token, not a client-supplied value.
    public async Task<IReadOnlyList<PantryItem>> PantryItemsAsync(
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        var items = await pantryClient.GetForUserAsync(cancellationToken);
        return items
            .Select(i => new PantryItem
            {
                IngredientId = i.IngredientId,
                IngredientName = i.IngredientName,
                UpdatedAt = i.UpdatedAt
            })
            .ToList();
    }

    // Single source of truth for the ingredient default-unit catalog lives in recipe-service
    // (RecipeService.Domain.MeasurementUnits) -- the web-client dropdown and its
    // fractional-vs-decimal quantity input behavior are driven entirely from this instead of
    // keeping a second hardcoded copy on the frontend.
    public async Task<IReadOnlyList<MeasurementUnit>> UnitsAsync(
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var units = await recipeClient.GetUnitsAsync(cancellationToken);
        return units
            .Select(u => new MeasurementUnit
            {
                Code = u.Code,
                Label = u.Label,
                IsFractionalFriendly = u.IsFractionalFriendly
            })
            .ToList();
    }

    // Same reasoning as UnitsAsync: single source of truth lives in recipe-service
    // (RecipeService.Domain.IngredientCategories) -- the Manage Ingredients category dropdown
    // is driven entirely from this instead of keeping a second hardcoded copy on the frontend.
    public async Task<IReadOnlyList<IngredientCategory>> IngredientCategoriesAsync(
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var categories = await recipeClient.GetIngredientCategoriesAsync(cancellationToken);
        return categories
            .Select(c => new IngredientCategory
            {
                Code = c.Code,
                Label = c.Label
            })
            .ToList();
    }

    // "What can I cook" ranking: recipe-service already loads every recipe's ingredients in one
    // query to build the plain recipe list (RecipeRepository.GetAllAsync), so the match/missing
    // computation is done there rather than here -- doing it in Gateway would mean either an
    // extra full-detail round trip per recipe (N+1) or recipe-service shipping every recipe's
    // complete ingredient list over the wire just so Gateway can count them. Gateway's job here
    // is purely mapping the ranked result onto GraphQL types.
    public async Task<IReadOnlyList<RecipeMatch>> RecipeMatchesAsync(
        List<long> ingredientIds,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var matches = await recipeClient.GetMatchesAsync(ingredientIds, cancellationToken);
        return matches
            .Select(m => new RecipeMatch
            {
                Recipe = new RecipeSummary
                {
                    Id = m.Id,
                    AuthorId = m.AuthorId,
                    Title = m.Title,
                    Description = m.Description,
                    Servings = m.Servings,
                    PrepTimeMin = m.PrepTimeMin,
                    CookTimeMin = m.CookTimeMin,
                    ImageUrl = m.ImageUrl,
                    CreatedAt = m.CreatedAt
                },
                RequiredIngredientCount = m.RequiredIngredientCount,
                MatchedIngredientCount = m.MatchedIngredientCount,
                MissingIngredients = m.MissingIngredients
                    .Select(mi => new MissingMatchIngredient
                    {
                        IngredientId = mi.IngredientId,
                        IngredientName = mi.IngredientName
                    })
                    .ToList()
            })
            .ToList();
    }

    // Standalone per-ingredient lookup, separate from RecipeIngredient.nearbyStores -- that
    // field is nested under a recipe's full ingredients list, so using it for a single
    // ingredient would mean re-fetching (and re-querying sourcing-service for) every other
    // ingredient too. This lets a client look up just the one ingredient it needs.
    public async Task<IReadOnlyList<StoreOffer>> NearbyStoresAsync(
        string ingredientName,
        double lat,
        double lng,
        [Service] ISourcingServiceClient sourcingClient,
        CancellationToken cancellationToken)
    {
        var nearby = await sourcingClient.GetNearbyAsync(ingredientName, lat, lng, cancellationToken);
        return StoreOfferMapper.ToGraphQl(nearby.Results);
    }
}
