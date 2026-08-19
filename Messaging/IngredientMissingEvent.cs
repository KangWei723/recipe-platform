namespace Messaging;

public static class QStashTopics
{
    public const string IngredientMissing = "ingredient-missing";
}

public record IngredientMissingEvent(
    long UserId,
    long RecipeId,
    long IngredientId,
    string IngredientName,
    DateTimeOffset OccurredAt
);
