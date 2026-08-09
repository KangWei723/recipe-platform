namespace Gateway.Models;

public class PantryItem
{
    public required long Id { get; init; }
    public required long UserId { get; init; }
    public required long IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
