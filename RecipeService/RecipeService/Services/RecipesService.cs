using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;

namespace RecipeService.Services;

public class RecipesService(
    IRecipeRepository recipeRepository,
    IUserRepository userRepository,
    IIngredientRepository ingredientRepository) : IRecipesService
{
    public async Task<RecipeDetailResponse> GetByIdAsync(long id)
    {
        var recipe = await recipeRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Recipe {id} not found");
        return ToDetailResponse(recipe);
    }

    public async Task<List<RecipeSummaryResponse>> GetAllAsync()
    {
        var recipes = await recipeRepository.GetAllAsync();
        return recipes.Select(ToSummaryResponse).ToList();
    }

    public async Task<RecipeDetailResponse> CreateAsync(CreateRecipeRequest request, long authorId)
    {
        var author = await userRepository.GetByIdAsync(authorId)
            ?? throw new ValidationException($"Author {authorId} does not exist");

        var ingredientIds = request.Ingredients.Select(i => i.IngredientId).Distinct().ToList();
        var existingIngredients = await ingredientRepository.GetByIdsAsync(ingredientIds);
        if (existingIngredients.Count != ingredientIds.Count)
        {
            var missing = ingredientIds.Except(existingIngredients.Select(i => i.Id));
            throw new ValidationException($"Unknown ingredient id(s): {string.Join(", ", missing)}");
        }

        var recipe = new Recipe
        {
            AuthorId = author.Id,
            Title = request.Title,
            Description = request.Description,
            Servings = request.Servings,
            PrepTimeMin = request.PrepTimeMin,
            CookTimeMin = request.CookTimeMin,
            ImageUrl = request.ImageUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = request.Steps
                .Select(s => new RecipeStep
                {
                    StepNumber = s.StepNumber,
                    Instruction = s.Instruction,
                    TimerSeconds = s.TimerSeconds
                })
                .ToList(),
            Ingredients = request.Ingredients
                .Select(i => new RecipeIngredient
                {
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Optional = i.Optional
                })
                .ToList()
        };

        var created = await recipeRepository.AddAsync(recipe);
        var withNames = await recipeRepository.GetByIdAsync(created.Id)
            ?? throw new NotFoundException($"Recipe {created.Id} not found after create");
        return ToDetailResponse(withNames);
    }

    private static RecipeSummaryResponse ToSummaryResponse(Recipe recipe) =>
        new(
            recipe.Id,
            recipe.AuthorId,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepTimeMin,
            recipe.CookTimeMin,
            recipe.ImageUrl,
            recipe.CreatedAt
        );

    private static RecipeDetailResponse ToDetailResponse(Recipe recipe) =>
        new(
            recipe.Id,
            recipe.AuthorId,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepTimeMin,
            recipe.CookTimeMin,
            recipe.ImageUrl,
            recipe.CreatedAt,
            recipe.Steps
                .OrderBy(s => s.StepNumber)
                .Select(s => new RecipeStepResponse(s.Id, s.StepNumber, s.Instruction, s.TimerSeconds))
                .ToList(),
            recipe.Ingredients
                .Select(i => new RecipeIngredientResponse(
                    i.Id, i.IngredientId, i.Ingredient!.Name, i.Quantity, i.Unit, i.Optional))
                .ToList()
        );
}
