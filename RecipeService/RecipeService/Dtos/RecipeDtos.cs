using System.ComponentModel.DataAnnotations;

namespace RecipeService.Dtos;

public record CreateRecipeStepRequest(
    [Required, Range(1, int.MaxValue)] int StepNumber,
    [Required] string Instruction,
    int? TimerSeconds
);

public record CreateRecipeIngredientRequest(
    [Required] long IngredientId,
    [Required, Range(0.01, double.MaxValue)] decimal Quantity,
    [Required, MaxLength(50)] string Unit,
    bool Optional
);

public record CreateRecipeRequest(
    [Required, MaxLength(255)] string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    List<CreateRecipeStepRequest> Steps,
    List<CreateRecipeIngredientRequest> Ingredients
)
{
    // If the client omits "steps"/"ingredients" entirely, System.Text.Json's
    // constructor-based deserialization falls back to these defaults instead
    // of binding null — keeps CreateAsync's LINQ over these lists NRE-free.
    public List<CreateRecipeStepRequest> Steps { get; init; } = Steps ?? [];
    public List<CreateRecipeIngredientRequest> Ingredients { get; init; } = Ingredients ?? [];
}

public record UpdateRecipeRequest(
    [Required, MaxLength(255)] string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    List<CreateRecipeStepRequest> Steps,
    List<CreateRecipeIngredientRequest> Ingredients
)
{
    // Same rationale as CreateRecipeRequest: keep LINQ over these lists NRE-free
    // even if the client omits "steps"/"ingredients" entirely.
    public List<CreateRecipeStepRequest> Steps { get; init; } = Steps ?? [];
    public List<CreateRecipeIngredientRequest> Ingredients { get; init; } = Ingredients ?? [];
}

public record RecipeStepResponse(
    long Id,
    int StepNumber,
    string Instruction,
    int? TimerSeconds
);

public record RecipeIngredientResponse(
    long Id,
    long IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    bool Optional
);

public record RecipeSummaryResponse(
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

public record RecipeDetailResponse(
    long Id,
    long AuthorId,
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMin,
    int? CookTimeMin,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    List<RecipeStepResponse> Steps,
    List<RecipeIngredientResponse> Ingredients
);
