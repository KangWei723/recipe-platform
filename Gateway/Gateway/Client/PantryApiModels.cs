namespace Gateway.Client;

// Minimal client-side mirrors of pantry-service's JSON response contracts.
// See RecipeApiModels.cs for why these are duplicated rather than shared.

public record MissingIngredientDto(
    long IngredientId,
    string IngredientName
);

public record MissingIngredientsDto(
    long RecipeId,
    string RecipeTitle,
    List<MissingIngredientDto> MissingIngredients
);

public record PantryItemDto(
    long IngredientId,
    string IngredientName,
    DateTimeOffset UpdatedAt
);

public record UpsertPantryItemDto(
    long IngredientId
);
