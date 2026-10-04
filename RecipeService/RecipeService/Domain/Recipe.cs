namespace RecipeService.Domain;

public class Recipe
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int? Servings { get; set; }
    public int? PrepTimeMin { get; set; }
    public int? CookTimeMin { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Tips { get; set; } = new();
    public string? Pairing { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? Author { get; set; }
    public List<RecipeStep> Steps { get; set; } = new();
    public List<RecipeIngredient> Ingredients { get; set; } = new();
}
