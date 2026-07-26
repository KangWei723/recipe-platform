namespace SubstitutionService.Domain;

public class Substitution
{
    public required string IngredientName { get; set; }
    public required string SubstituteName { get; set; }
    public double Ratio { get; set; } = 1.0;
    public IReadOnlyList<string> Contexts { get; set; } = [];
    public double Confidence { get; set; }
}

public class RankedSubstitute
{
    public required string SubstituteName { get; set; }
    public double Ratio { get; set; }
    public IReadOnlyList<string> Contexts { get; set; } = [];
    public double Confidence { get; set; }
}
