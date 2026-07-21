namespace RecipeService.Domain;

public class User
{
    public long Id { get; set; }
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
