namespace Gateway.GraphQL;

public class CreateRecipeStepInput
{
    public required int StepNumber { get; init; }
    public required string Instruction { get; init; }
    public int? TimerSeconds { get; init; }
}

public class CreateRecipeIngredientInput
{
    public required long IngredientId { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public bool Optional { get; init; }
}
