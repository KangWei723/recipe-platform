namespace PantryService.Client;

public interface IRecipeServiceClient
{
    Task<IngredientDto?> GetIngredientAsync(long ingredientId, CancellationToken cancellationToken = default);
    Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the numeric user id for whoever's bearer token this request is carrying, via
    /// recipe-service's JIT-provisioning endpoint. The Authorization header is forwarded onto
    /// this call (see AuthHeaderForwardingHandler), so recipe-service validates the same token.
    /// </summary>
    Task<UserDto> ResolveCurrentUserAsync(CancellationToken cancellationToken = default);
}
