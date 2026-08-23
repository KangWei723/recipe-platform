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
        var usageCount = await repository.CountRecipeUsagesAsync(id);
        return ToResponse(ingredient, usageCount);
    }

    public async Task<List<IngredientResponse>> GetAllAsync()
    {
        var ingredients = await repository.GetAllAsync();
        var usageCounts = await repository.CountAllRecipeUsagesAsync();
        return ingredients.Select(i => ToResponse(i, usageCounts.GetValueOrDefault(i.Id, 0))).ToList();
    }

    public async Task<IngredientResponse> CreateAsync(CreateIngredientRequest request)
    {
        EnsureValidUnit(request.DefaultUnit);
        EnsureValidCategory(request.Category);

        var ingredient = new Ingredient
        {
            Name = request.Name,
            Category = request.Category,
            DefaultUnit = request.DefaultUnit
        };

        var created = await repository.AddAsync(ingredient);
        // A brand-new ingredient can't be referenced by any recipe yet.
        return ToResponse(created, usageCount: 0);
    }

    public async Task<IngredientResponse> UpdateAsync(long id, UpdateIngredientRequest request)
    {
        EnsureValidUnit(request.DefaultUnit);
        EnsureValidCategory(request.Category);

        var updated = await repository.UpdateAsync(id, ingredient =>
        {
            ingredient.Name = request.Name;
            ingredient.Category = request.Category;
            ingredient.DefaultUnit = request.DefaultUnit;
        }) ?? throw new NotFoundException($"Ingredient {id} not found");

        var usageCount = await repository.CountRecipeUsagesAsync(id);
        return ToResponse(updated, usageCount);
    }

    // Ingredient units are standardized at the source (this service), not left to free text --
    // the web-client dropdown already restricts to this same catalog, but the server is the
    // real boundary since any other caller of this API would otherwise bypass it.
    private static void EnsureValidUnit(string unit)
    {
        if (!MeasurementUnits.IsValid(unit))
        {
            throw new ValidationException($"Unknown default unit: {unit}");
        }
    }

    // Same reasoning as EnsureValidUnit: category is a curated catalog, not free text, and the
    // server is the real enforcement boundary regardless of what the web-client dropdown allows.
    private static void EnsureValidCategory(string category)
    {
        if (!IngredientCategories.IsValid(category))
        {
            throw new ValidationException($"Unknown category: {category}");
        }
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

    private static IngredientResponse ToResponse(Ingredient ingredient, int usageCount) =>
        new(ingredient.Id, ingredient.Name, ingredient.Category, ingredient.DefaultUnit, usageCount);
}
