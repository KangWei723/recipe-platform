namespace Gateway.Client;

// Minimal client-side mirrors of pantry-service's JSON response contracts.
// See RecipeApiModels.cs for why these are duplicated rather than shared.

public record MissingIngredientDto(
    long IngredientId,
    string IngredientName,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    string Unit
);

public record MissingIngredientsDto(
    long RecipeId,
    string RecipeTitle,
    List<MissingIngredientDto> MissingIngredients
);
