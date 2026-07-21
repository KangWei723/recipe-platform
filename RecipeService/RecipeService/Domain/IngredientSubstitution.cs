namespace RecipeService.Domain;

public class IngredientSubstitution
{
    public long Id { get; set; }
    public long IngredientId { get; set; }
    public long SubstituteId { get; set; }
    public decimal Ratio { get; set; } = 1.0m;
    public string? Context { get; set; }

    public Ingredient? Ingredient { get; set; }
    public Ingredient? Substitute { get; set; }
}
