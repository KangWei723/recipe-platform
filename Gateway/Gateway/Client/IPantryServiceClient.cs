namespace Gateway.Client;

public interface IPantryServiceClient
{
    // No userId parameter: pantry-service derives the caller from the forwarded bearer
    // token's own claims, not from anything Gateway supplies.
    Task<MissingIngredientsDto> GetMissingIngredientsAsync(
        long recipeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PantryItemDto>> GetForUserAsync(CancellationToken cancellationToken = default);

    Task<PantryItemDto> UpsertAsync(UpsertPantryItemDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long itemId, CancellationToken cancellationToken = default);
}
