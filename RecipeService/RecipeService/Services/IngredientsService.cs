using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;

namespace RecipeService.Services;

public class IngredientsService(IIngredientRepository repository) : IIngredientsService
{
    public async Task<IngredientResponse> GetByIdAsync(long id)
    {
        var ingredient = await repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Ingredient {id} not found");
        return ToResponse(ingredient);
    }

    public async Task<List<IngredientResponse>> GetAllAsync()
    {
        var ingredients = await repository.GetAllAsync();
        return ingredients.Select(ToResponse).ToList();
    }

    public async Task<IngredientResponse> CreateAsync(CreateIngredientRequest request)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Category = request.Category,
            DefaultUnit = request.DefaultUnit
        };

        var created = await repository.AddAsync(ingredient);
        return ToResponse(created);
    }

    private static IngredientResponse ToResponse(Ingredient ingredient) =>
        new(ingredient.Id, ingredient.Name, ingredient.Category, ingredient.DefaultUnit);
}
