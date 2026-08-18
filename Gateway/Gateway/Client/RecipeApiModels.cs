namespace Gateway.Client;

// Minimal client-side mirrors of recipe-service's JSON response contracts.
// Deliberately duplicated rather than shared via a common assembly: each
// service owns its own contract (see docs/design.md — services talk over
// HTTP, never a shared schema or shared code).

public record RecipeStepDto(
    long Id,
    int StepNumber,
    string Instruction,
    int? TimerSeconds
);

public record RecipeIngredientDto(
    long Id,
    long IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    bool Optional
);

public record RecipeDetailDto(
    long Id,
    long AuthorId,
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    List<RecipeStepDto> Steps,
    List<RecipeIngredientDto> Ingredients
);

public record RecipeSummaryDto(
    long Id,
    long AuthorId,
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    DateTimeOffset CreatedAt
);

public record IngredientDto(
    long Id,
    string Name,
    string? Category,
    string DefaultUnit
);

public record CreateRecipeStepDto(
    int StepNumber,
    string Instruction,
    int? TimerSeconds
);

public record CreateRecipeIngredientDto(
    long IngredientId,
    decimal Quantity,
    string Unit,
    bool Optional
);

public record CreateRecipeDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    List<CreateRecipeStepDto> Steps,
    List<CreateRecipeIngredientDto> Ingredients
);

public record UpdateRecipeDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    List<CreateRecipeStepDto> Steps,
    List<CreateRecipeIngredientDto> Ingredients
);

public record CreateIngredientDto(
    string Name,
    string? Category,
    string DefaultUnit
);

public record UpdateIngredientDto(
    string Name,
    string? Category,
    string DefaultUnit
);
