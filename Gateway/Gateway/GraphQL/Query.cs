using Gateway.Client;
using Gateway.Models;
using HotChocolate;

namespace Gateway.GraphQL;

public class Query
{
    // Core query: recipe + ingredients + pantry status, resolved together.
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

        // GetMissingIngredientsAsync only flags *required* ingredients (see PantryItemsService --
        // it deliberately excludes optional ones, since "missing" there means "blocks cooking /
        // needs sourcing"), so it's kept here for its ingredient.missing event side effect, not
        // used to compute InPantry below -- an optional ingredient absent from that list is not
        // the same thing as an optional ingredient the user actually owns. InPantry instead comes
        // from the user's actual pantry contents, fetched independently alongside it.
        var missingTask = pantryClient.GetMissingIngredientsAsync(id, cancellationToken);
        var pantryItemsTask = pantryClient.GetForUserAsync(cancellationToken);
        await Task.WhenAll(missingTask, pantryItemsTask);

        var pantryIngredientIds = pantryItemsTask.Result
            .Select(p => p.IngredientId)
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
                    InPantry = pantryIngredientIds.Contains(i.IngredientId)
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

    // Confirmed, per-ingredient lookup (e.g. Kroger's real product/price search) -- called
    // on-demand for one ingredient at a time (imperative client.query() per button click), not
    // nested under the recipe's ingredients list, since re-fetching the whole recipe just to
    // check one ingredient's availability would be wasteful.
    public async Task<IReadOnlyList<StoreOffer>> ConfirmedStoreOfferAsync(
        string ingredientName,
        double lat,
        double lng,
        [Service] ISourcingServiceClient sourcingClient,
        CancellationToken cancellationToken)
    {
        var nearby = await sourcingClient.GetConfirmedNearbyAsync(ingredientName, lat, lng, cancellationToken);
        return StoreOfferMapper.ToGraphQl(nearby.Results);
    }

    // General, ingredient-agnostic "nearby stores" lookup (e.g. Google Places) -- not scoped to
    // any one ingredient, so callers fetch it once rather than once per missing ingredient.
    public async Task<IReadOnlyList<StoreOffer>> NearbyStoresGeneralAsync(
        double lat,
        double lng,
        [Service] ISourcingServiceClient sourcingClient,
        CancellationToken cancellationToken)
    {
        var nearby = await sourcingClient.GetGeneralNearbyAsync(lat, lng, cancellationToken);
        return StoreOfferMapper.ToGraphQl(nearby.Results);
    }
}
