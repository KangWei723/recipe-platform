namespace PantryService.Domain;

public class PantryItem
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = null!;
    public DateOnly? ExpiryDate { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
