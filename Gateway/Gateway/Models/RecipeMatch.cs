namespace Gateway.Models;

public class RecipeMatch
{
    public required RecipeSummary Recipe { get; init; }
    public required int RequiredIngredientCount { get; init; }
    public required int MatchedIngredientCount { get; init; }
    public required IReadOnlyList<MissingMatchIngredient> MissingIngredients { get; init; }
}

public class MissingMatchIngredient
{
    public required long IngredientId { get; init; }
    public required string IngredientName { get; init; }
}
