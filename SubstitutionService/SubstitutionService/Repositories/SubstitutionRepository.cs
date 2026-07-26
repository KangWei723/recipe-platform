using Neo4j.Driver;
using SubstitutionService.Domain;

namespace SubstitutionService.Repositories;

public class SubstitutionRepository(IDriver driver) : ISubstitutionRepository
{
    public async Task<Substitution> CreateAsync(
        string ingredientName,
        string substituteName,
        double ratio,
        IReadOnlyList<string> contexts,
        double confidence)
    {
        await using var session = driver.AsyncSession();

        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MERGE (i:Ingredient {name: $ingredientName})
                MERGE (s:Ingredient {name: $substituteName})
                MERGE (s)-[r:SUBSTITUTES_FOR]->(i)
                SET r.ratio = $ratio, r.contexts = $contexts, r.confidence = $confidence
                RETURN i.name AS ingredientName, s.name AS substituteName,
                       r.ratio AS ratio, r.contexts AS contexts, r.confidence AS confidence
                """,
                new
                {
                    ingredientName,
                    substituteName,
                    ratio,
                    contexts = contexts.ToArray(),
                    confidence
                });

            var record = await cursor.SingleAsync();
            return MapSubstitution(record);
        });
    }

    public async Task<IReadOnlyList<RankedSubstitute>> GetRankedSubstitutesAsync(string ingredientName, string? context)
    {
        await using var session = driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (s:Ingredient)-[r:SUBSTITUTES_FOR]->(i:Ingredient {name: $ingredientName})
                WHERE $context IS NULL OR $context IN r.contexts
                RETURN s.name AS substituteName, r.ratio AS ratio, r.contexts AS contexts, r.confidence AS confidence
                ORDER BY r.confidence DESC, r.ratio ASC
                """,
                new { ingredientName, context });

            var records = await cursor.ToListAsync();
            return (IReadOnlyList<RankedSubstitute>)records.Select(MapRankedSubstitute).ToList();
        });
    }

    private static Substitution MapSubstitution(IRecord record) => new()
    {
        IngredientName = record["ingredientName"].As<string>(),
        SubstituteName = record["substituteName"].As<string>(),
        Ratio = record["ratio"].As<double>(),
        Contexts = record["contexts"].As<List<string>>(),
        Confidence = record["confidence"].As<double>()
    };

    private static RankedSubstitute MapRankedSubstitute(IRecord record) => new()
    {
        SubstituteName = record["substituteName"].As<string>(),
        Ratio = record["ratio"].As<double>(),
        Contexts = record["contexts"].As<List<string>>(),
        Confidence = record["confidence"].As<double>()
    };
}
