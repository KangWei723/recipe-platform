using SubstitutionService.Dtos;
using SubstitutionService.Exceptions;
using SubstitutionService.Repositories;

namespace SubstitutionService.Services;

public class SubstitutionsService(ISubstitutionRepository repository) : ISubstitutionsService
{
    public async Task<SubstitutionResponse> CreateAsync(CreateSubstitutionRequest request)
    {
        if (string.Equals(request.IngredientName, request.SubstituteName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("An ingredient cannot substitute for itself.");
        }

        var contexts = request.Contexts ?? [];
        var created = await repository.CreateAsync(
            request.IngredientName,
            request.SubstituteName,
            request.Ratio,
            contexts,
            request.Confidence);

        return new SubstitutionResponse(
            created.IngredientName,
            created.SubstituteName,
            created.Ratio,
            created.Contexts,
            created.Confidence);
    }

    public async Task<IReadOnlyList<RankedSubstituteResponse>> GetRankedSubstitutesAsync(string ingredientName, string? context)
    {
        var ranked = await repository.GetRankedSubstitutesAsync(ingredientName, context);

        return ranked
            .Select(r => new RankedSubstituteResponse(r.SubstituteName, r.Ratio, r.Contexts, r.Confidence))
            .ToList();
    }
}
