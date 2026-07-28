using Gateway.Client;
using HotChocolate;

namespace Gateway.GraphQL;

// Substitution-service has no bulk-lookup endpoint, so batching here means
// firing the per-ingredient calls in parallel and deduplicating repeated
// names within a single GraphQL request, rather than reducing HTTP calls.
internal static class SubstitutionDataLoaders
{
    [DataLoader]
    public static async Task<Dictionary<string, IReadOnlyList<RankedSubstituteDto>>> GetRankedSubstitutesByNameAsync(
        IReadOnlyList<string> ingredientNames,
        ISubstitutionServiceClient client,
        CancellationToken cancellationToken)
    {
        var pairs = await Task.WhenAll(ingredientNames.Select(async name =>
        {
            var ranked = await client.GetRankedSubstitutesAsync(name, cancellationToken);
            return (name, ranked);
        }));

        return pairs.ToDictionary(p => p.name, p => p.ranked);
    }
}
