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
}
