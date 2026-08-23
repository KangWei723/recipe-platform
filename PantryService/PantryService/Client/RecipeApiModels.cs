namespace PantryService.Client;

// Minimal client-side mirrors of recipe-service's JSON response contracts.
// Deliberately duplicated rather than shared via a common assembly: each
// service owns its own contract, and Pantry only depends on the shape of the
// fields it actually consumes (see docs/design.md — services talk over HTTP,
// never a shared schema or shared code).

public record IngredientDto(long Id, string Name, string Category, string DefaultUnit);

public record UserDto(long Id, string Email, string Name);

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
    List<RecipeIngredientDto> Ingredients
);
