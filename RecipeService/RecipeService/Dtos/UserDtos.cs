using System.ComponentModel.DataAnnotations;

namespace RecipeService.Dtos;

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MaxLength(255)] string Name
);

public record UserResponse(
    long Id,
    string Email,
    string Name,
    DateTimeOffset CreatedAt
);
