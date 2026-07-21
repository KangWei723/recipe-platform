using System.ComponentModel.DataAnnotations;

namespace RecipeService.Dtos;

public record CreateIngredientRequest(
    [Required, MaxLength(255)] string Name,
    [MaxLength(100)] string? Category,
    [Required, MaxLength(50)] string DefaultUnit
);

public record IngredientResponse(
    long Id,
    string Name,
    string? Category,
    string DefaultUnit
);
