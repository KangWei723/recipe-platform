namespace Gateway.Models;

public class Recipe
{
    public required long Id { get; init; }
    public required long AuthorId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public int? Servings { get; init; }
    public int? PrepTimeMin { get; init; }
    public int? CookTimeMin { get; init; }
    public string? ImageUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<RecipeStep> Steps { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
}

public class RecipeStep
{
    public required long Id { get; init; }
    public required int StepNumber { get; init; }
    public required string Instruction { get; init; }
    public int? TimerSeconds { get; init; }
}

public class RecipeIngredient
{
    public required long Id { get; init; }
    public required long IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required bool Optional { get; init; }

    // Resolved once per recipe query from pantry-service's missing-ingredients
    // check, rather than re-checked per ingredient (see Query.GetRecipeAsync).
    public required bool InPantry { get; init; }
}
