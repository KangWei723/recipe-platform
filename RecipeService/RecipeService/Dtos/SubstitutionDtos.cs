using System.ComponentModel.DataAnnotations;

namespace RecipeService.Dtos;

public record CreateSubstitutionRequest(
    [Required] long IngredientId,
    [Required] long SubstituteId,
    [Range(0.0001, double.MaxValue)] decimal Ratio,
    string? Context
);

public record SubstitutionResponse(
    long Id,
    long IngredientId,
    long SubstituteId,
    string SubstituteName,
    decimal Ratio,
    string? Context
);
