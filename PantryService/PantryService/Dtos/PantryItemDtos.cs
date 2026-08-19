using System.ComponentModel.DataAnnotations;

namespace PantryService.Dtos;

public record UpsertPantryItemRequest(
    [Required] long IngredientId
);

public record PantryItemResponse(
    long IngredientId,
    string IngredientName,
    DateTimeOffset UpdatedAt
);
