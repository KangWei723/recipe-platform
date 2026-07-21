using RecipeService.Dtos;

namespace RecipeService.Services;

public interface ISubstitutionsService
{
    Task<List<SubstitutionResponse>> GetForIngredientAsync(long ingredientId);
    Task<SubstitutionResponse> CreateAsync(CreateSubstitutionRequest request);
}
