using Gateway.Client;
using Gateway.Models;
using HotChocolate;
using HotChocolate.Types;

namespace Gateway.GraphQL;

[ExtendObjectType(typeof(RecipeIngredient))]
public class RecipeIngredientResolvers
{
    public async Task<IReadOnlyList<Substitution>> GetSubstitutionsAsync(
        [Parent] RecipeIngredient ingredient,
        IRankedSubstitutesByNameDataLoader rankedSubstitutesByName,
        CancellationToken cancellationToken)
    {
        var ranked = await rankedSubstitutesByName.LoadAsync(ingredient.IngredientName, cancellationToken);
        return (ranked ?? [])
            .Select(r => new Substitution
            {
                SubstituteName = r.SubstituteName,
                Ratio = r.Ratio,
                Contexts = r.Contexts,
                Confidence = r.Confidence
            })
            .ToList();
    }

    public async Task<IReadOnlyList<StoreOffer>> GetNearbyStoresAsync(
        [Parent] RecipeIngredient ingredient,
        double lat,
        double lng,
        [Service] ISourcingServiceClient sourcingClient,
        CancellationToken cancellationToken)
    {
        var nearby = await sourcingClient.GetNearbyAsync(ingredient.IngredientName, lat, lng, cancellationToken);
        return nearby.Results
            .Select(r => new StoreOffer
            {
                ProviderName = r.ProviderName,
                StoreName = r.StoreName,
                Address = r.Address,
                Lat = r.Lat,
                Lng = r.Lng,
                Price = r.Price,
                Currency = r.Currency,
                IsSimulated = r.IsSimulated
            })
            .ToList();
    }
}
