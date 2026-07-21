namespace PantryService.Client;

public interface IRecipeServiceClient
{
    Task<IngredientDto?> GetIngredientAsync(long ingredientId, CancellationToken cancellationToken = default);
    Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default);
}
