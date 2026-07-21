using System.ComponentModel.DataAnnotations;

namespace PantryService.Dtos;

public record UpsertPantryItemRequest(
    [Required] long IngredientId,
    [Required, Range(0, double.MaxValue)] decimal Quantity,
    [Required, MaxLength(50)] string Unit,
    DateOnly? ExpiryDate
);

public record PantryItemResponse(
    long Id,
    long UserId,
    long IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    DateOnly? ExpiryDate,
    DateTimeOffset UpdatedAt
);
