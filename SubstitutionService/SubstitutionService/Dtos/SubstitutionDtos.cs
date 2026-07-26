using System.ComponentModel.DataAnnotations;

namespace SubstitutionService.Dtos;

public record CreateSubstitutionRequest(
    [Required] string IngredientName,
    [Required] string SubstituteName,
    [Range(0.0001, double.MaxValue)] double Ratio,
    List<string>? Contexts,
    [Range(0.0, 1.0)] double Confidence
);

public record SubstitutionResponse(
    string IngredientName,
    string SubstituteName,
    double Ratio,
    IReadOnlyList<string> Contexts,
    double Confidence
);

public record RankedSubstituteResponse(
    string SubstituteName,
    double Ratio,
    IReadOnlyList<string> Contexts,
    double Confidence
);
