using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IIngredientsService
{
    Task<IngredientResponse> GetByIdAsync(long id);
    Task<List<IngredientResponse>> GetAllAsync();
    Task<IngredientResponse> CreateAsync(CreateIngredientRequest request);
}
