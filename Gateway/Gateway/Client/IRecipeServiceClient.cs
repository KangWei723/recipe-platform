namespace Gateway.Client;

public interface IRecipeServiceClient
{
    Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default);
}
