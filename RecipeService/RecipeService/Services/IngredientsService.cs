using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<IngredientResponse> UpdateAsync(long id, UpdateIngredientRequest request)
    {
        var updated = await repository.UpdateAsync(id, ingredient =>
        {
            ingredient.Name = request.Name;
            ingredient.Category = request.Category;
            ingredient.DefaultUnit = request.DefaultUnit;
        }) ?? throw new NotFoundException($"Ingredient {id} not found");

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id)
    {
        var usageCount = await repository.CountRecipeUsagesAsync(id);
        if (usageCount > 0)
        {
            throw new ConflictException(
                $"Cannot delete ingredient: used in {usageCount} recipe{(usageCount == 1 ? "" : "s")}");
        }

        try
        {
            var deleted = await repository.DeleteAsync(id);
            if (!deleted)
            {
                throw new NotFoundException($"Ingredient {id} not found");
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Lost a race with a recipe that started referencing this ingredient after the
            // usage-count check above but before this delete committed -- surface the same
            // friendly conflict instead of a raw constraint-violation 500.
            throw new ConflictException("Cannot delete ingredient: now in use by a recipe");
        }
    }

    private static IngredientResponse ToResponse(Ingredient ingredient) =>
        new(ingredient.Id, ingredient.Name, ingredient.Category, ingredient.DefaultUnit);
}
