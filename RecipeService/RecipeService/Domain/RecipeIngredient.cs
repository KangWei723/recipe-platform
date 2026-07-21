namespace RecipeService.Domain;

public class RecipeIngredient
{
    public long Id { get; set; }
    public long RecipeId { get; set; }
    public long IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = null!;
    public bool Optional { get; set; }

    public Recipe? Recipe { get; set; }
    public Ingredient? Ingredient { get; set; }
}
