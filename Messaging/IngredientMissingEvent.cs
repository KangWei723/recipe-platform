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
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    string Unit,
    DateTimeOffset OccurredAt
);
