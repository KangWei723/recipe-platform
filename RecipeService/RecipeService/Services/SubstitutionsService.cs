using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;

namespace RecipeService.Services;

public class SubstitutionsService(
    ISubstitutionRepository substitutionRepository,
    IIngredientRepository ingredientRepository) : ISubstitutionsService
{
    public async Task<List<SubstitutionResponse>> GetForIngredientAsync(long ingredientId)
    {
        var substitutions = await substitutionRepository.GetForIngredientAsync(ingredientId);
        return substitutions.Select(ToResponse).ToList();
    }

    public async Task<SubstitutionResponse> CreateAsync(CreateSubstitutionRequest request)
    {
        if (request.IngredientId == request.SubstituteId)
        {
            throw new ValidationException("An ingredient cannot substitute for itself");
        }

        var ids = new[] { request.IngredientId, request.SubstituteId };
        var existing = await ingredientRepository.GetByIdsAsync(ids);
        if (existing.Count != 2)
        {
            throw new ValidationException("Both ingredientId and substituteId must reference existing ingredients");
        }

        var substitution = new IngredientSubstitution
        {
            IngredientId = request.IngredientId,
            SubstituteId = request.SubstituteId,
            Ratio = request.Ratio,
            Context = request.Context
        };

        var created = await substitutionRepository.AddAsync(substitution);
        created.Substitute = existing.First(i => i.Id == request.SubstituteId);
        return ToResponse(created);
    }

    private static SubstitutionResponse ToResponse(IngredientSubstitution substitution) =>
        new(
            substitution.Id,
            substitution.IngredientId,
            substitution.SubstituteId,
            substitution.Substitute!.Name,
            substitution.Ratio,
            substitution.Context
        );
}
