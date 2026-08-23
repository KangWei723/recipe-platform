namespace Gateway.Models;

public class Ingredient
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string DefaultUnit { get; init; }
    public required int UsageCount { get; init; }
}
