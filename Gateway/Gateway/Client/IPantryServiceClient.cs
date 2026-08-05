namespace Gateway.Client;

public interface IPantryServiceClient
{
    // No userId parameter: pantry-service derives the caller from the forwarded bearer
    // token's own claims, not from anything Gateway supplies.
    Task<MissingIngredientsDto> GetMissingIngredientsAsync(
        long recipeId, CancellationToken cancellationToken = default);
}
