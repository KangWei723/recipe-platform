namespace RecipeService.Domain;

public class RecipeStep
{
    public long Id { get; set; }
    public long RecipeId { get; set; }
    public int StepNumber { get; set; }
    public string Instruction { get; set; } = null!;
    public int? TimerSeconds { get; set; }
    public string? ImageUrl { get; set; }

    public Recipe? Recipe { get; set; }
}
