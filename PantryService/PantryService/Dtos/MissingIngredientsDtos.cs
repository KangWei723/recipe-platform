namespace PantryService.Dtos;

// Diffs a recipe's ingredient list against the user's pantry. This is the
// Phase 1 stand-in for what Phase 2 turns into an async flow: pantry
// service would emit an `ingredient.missing` event per entry here instead
// of returning the list synchronously (see docs/design.md).
public record MissingIngredientResponse(
    long IngredientId,
    string IngredientName,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    string Unit
);

public record MissingIngredientsResponse(
    long RecipeId,
    string RecipeTitle,
    List<MissingIngredientResponse> MissingIngredients
);
