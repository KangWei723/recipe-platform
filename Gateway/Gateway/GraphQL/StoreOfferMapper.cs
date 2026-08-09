using Gateway.Client;
using Gateway.Models;

namespace Gateway.GraphQL;

// Shared between Query.NearbyStoresAsync (standalone per-ingredient lookup) and
// RecipeIngredientResolvers.GetNearbyStoresAsync (nested under a recipe's ingredients) so the
// DTO -> GraphQL model mapping isn't duplicated between the two call sites.
internal static class StoreOfferMapper
{
    public static IReadOnlyList<StoreOffer> ToGraphQl(IReadOnlyList<StoreOfferDto> offers) =>
        offers
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
