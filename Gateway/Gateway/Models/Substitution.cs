namespace Gateway.Models;

public class Substitution
{
    public required string SubstituteName { get; init; }
    public required double Ratio { get; init; }
    public required IReadOnlyList<string> Contexts { get; init; }
    public required double Confidence { get; init; }
}
