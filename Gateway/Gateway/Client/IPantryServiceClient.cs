namespace Gateway.Client;

public interface IPantryServiceClient
{
    Task<MissingIngredientsDto> GetMissingIngredientsAsync(
        long userId, long recipeId, CancellationToken cancellationToken = default);
}
