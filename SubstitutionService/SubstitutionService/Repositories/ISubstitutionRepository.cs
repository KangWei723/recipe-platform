using SubstitutionService.Domain;

namespace SubstitutionService.Repositories;

public interface ISubstitutionRepository
{
    Task<Substitution> CreateAsync(
        string ingredientName,
        string substituteName,
        double ratio,
        IReadOnlyList<string> contexts,
        double confidence);

    Task<IReadOnlyList<RankedSubstitute>> GetRankedSubstitutesAsync(string ingredientName, string? context);
}
