namespace Gateway.Models;

public class PantryItem
{
    public required long IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
