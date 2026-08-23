namespace Gateway.Client;

public interface IRecipeServiceClient
{
    Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecipeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeasurementUnitDto>> GetUnitsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecipeMatchDto>> GetMatchesAsync(
        IReadOnlyList<long> ingredientIds, CancellationToken cancellationToken = default);

    // No author argument: recipe-service derives the author from the caller's own forwarded
    // bearer token (UsersService.ResolveCurrentUserAsync), not from anything Gateway supplies.
    Task<RecipeDetailDto> CreateAsync(CreateRecipeDto request, CancellationToken cancellationToken = default);

    Task<RecipeDetailDto> UpdateAsync(long recipeId, UpdateRecipeDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long recipeId, CancellationToken cancellationToken = default);

    Task<IngredientDto> CreateIngredientAsync(CreateIngredientDto request, CancellationToken cancellationToken = default);

    Task<IngredientDto> UpdateIngredientAsync(long ingredientId, UpdateIngredientDto request, CancellationToken cancellationToken = default);

    Task DeleteIngredientAsync(long ingredientId, CancellationToken cancellationToken = default);
}
