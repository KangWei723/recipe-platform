using SubstitutionService.Dtos;

namespace SubstitutionService.Services;

public interface ISubstitutionsService
{
    Task<SubstitutionResponse> CreateAsync(CreateSubstitutionRequest request);

    Task<IReadOnlyList<RankedSubstituteResponse>> GetRankedSubstitutesAsync(string ingredientName, string? context);
}
