namespace RecipeService.Domain;

public class Ingredient
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Category { get; set; }
    public string DefaultUnit { get; set; } = null!;
}
