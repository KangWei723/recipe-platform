using Microsoft.Extensions.Options;
using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;

namespace RecipeService.Services;

public class RecipesService(
    IRecipeRepository recipeRepository,
    IUserRepository userRepository,
    IIngredientRepository ingredientRepository,
    IOptions<CloudinaryOptions> cloudinaryOptions) : IRecipesService
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

        await EnsureIngredientsExistAsync(request.Ingredients.Select(i => i.IngredientId));
        EnsureValidRecipeContent(request.ImageUrl, request.Steps, request.Tips, request.Pairing);

        var recipe = new Recipe
        {
            AuthorId = author.Id,
            Title = request.Title,
            Description = request.Description,
            Servings = request.Servings,
            PrepTimeMin = request.PrepTimeMin,
            CookTimeMin = request.CookTimeMin,
            ImageUrl = request.ImageUrl,
            Tips = request.Tips,
            Pairing = request.Pairing,
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = request.Steps
                .Select(s => new RecipeStep
                {
                    StepNumber = s.StepNumber,
                    Instruction = s.Instruction,
                    TimerSeconds = s.TimerSeconds,
                    ImageUrl = s.ImageUrl
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

    public async Task<RecipeDetailResponse> UpdateAsync(long id, UpdateRecipeRequest request)
    {
        await EnsureIngredientsExistAsync(request.Ingredients.Select(i => i.IngredientId));
        EnsureValidRecipeContent(request.ImageUrl, request.Steps, request.Tips, request.Pairing);

        var updated = await recipeRepository.UpdateAsync(
            id,
            request.Title,
            request.Description,
            request.Servings,
            request.PrepTimeMin,
            request.CookTimeMin,
            request.ImageUrl,
            request.Steps
                .Select(s => new RecipeStep
                {
                    StepNumber = s.StepNumber,
                    Instruction = s.Instruction,
                    TimerSeconds = s.TimerSeconds,
                    ImageUrl = s.ImageUrl
                })
                .ToList(),
            request.Ingredients
                .Select(i => new RecipeIngredient
                {
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Optional = i.Optional
                })
                .ToList(),
            request.Tips,
            request.Pairing
        ) ?? throw new NotFoundException($"Recipe {id} not found");

        var withNames = await recipeRepository.GetByIdAsync(updated.Id)
            ?? throw new NotFoundException($"Recipe {updated.Id} not found after update");
        return ToDetailResponse(withNames);
    }

    // Shared by CreateAsync/UpdateAsync: validates the hero image URL, every step's image URL,
    // and the tips/pairing size limits before anything touches the repository -- same fail-fast
    // positioning as EnsureIngredientsExistAsync above.
    private void EnsureValidRecipeContent(
        string? imageUrl, List<CreateRecipeStepRequest> steps, List<string> tips, string? pairing)
    {
        // Off by default -- see CloudinaryOptions.RestrictImageUrlsToOwnCloud.
        var requiredCloudName = cloudinaryOptions.Value.RestrictImageUrlsToOwnCloud
            ? cloudinaryOptions.Value.CloudName
            : null;

        ImageUrlValidator.EnsureValid(imageUrl, requiredCloudName);
        foreach (var step in steps)
        {
            ImageUrlValidator.EnsureValid(step.ImageUrl, requiredCloudName);
        }
        RecipeContentLimits.EnsureValidTips(tips);
        RecipeContentLimits.EnsureValidPairing(pairing);
    }

    public async Task DeleteAsync(long id)
    {
        var deleted = await recipeRepository.DeleteAsync(id);
        if (!deleted)
        {
            throw new NotFoundException($"Recipe {id} not found");
        }
    }

    // "What can I cook" matching: rank every recipe by how much of its required ingredient
    // list is covered by the caller's ingredient set. Reuses the same GetAllAsync query
    // RecipeSummaryResponse is built from (already loads Ingredients via Include), so this
    // adds no extra round trip to the database beyond what listing recipes already costs.
    public async Task<List<RecipeMatchResponse>> GetMatchesAsync(List<long> ingredientIds)
    {
        var haveIds = ingredientIds.ToHashSet();
        var recipes = await recipeRepository.GetAllAsync();

        return recipes
            .Select(recipe => ToMatchResponse(recipe, haveIds))
            .OrderBy(m => m.MissingIngredients.Count)
            .ThenByDescending(m => m.MatchedIngredientCount)
            .ToList();
    }

    private async Task EnsureIngredientsExistAsync(IEnumerable<long> ingredientIds)
    {
        var distinctIds = ingredientIds.Distinct().ToList();
        var existingIngredients = await ingredientRepository.GetByIdsAsync(distinctIds);
        if (existingIngredients.Count != distinctIds.Count)
        {
            var missing = distinctIds.Except(existingIngredients.Select(i => i.Id));
            throw new ValidationException($"Unknown ingredient id(s): {string.Join(", ", missing)}");
        }
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

    private static RecipeMatchResponse ToMatchResponse(Recipe recipe, HashSet<long> haveIds)
    {
        var required = recipe.Ingredients.Where(ri => !ri.Optional).ToList();
        var missing = required
            .Where(ri => !haveIds.Contains(ri.IngredientId))
            .Select(ri => new MissingMatchIngredientResponse(ri.IngredientId, ri.Ingredient!.Name))
            .ToList();

        return new RecipeMatchResponse(
            recipe.Id,
            recipe.AuthorId,
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.PrepTimeMin,
            recipe.CookTimeMin,
            recipe.ImageUrl,
            recipe.CreatedAt,
            RequiredIngredientCount: required.Count,
            MatchedIngredientCount: required.Count - missing.Count,
            MissingIngredients: missing
        );
    }

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
                .Select(s => new RecipeStepResponse(s.Id, s.StepNumber, s.Instruction, s.TimerSeconds, s.ImageUrl))
                .ToList(),
            recipe.Ingredients
                .Select(i => new RecipeIngredientResponse(
                    i.Id, i.IngredientId, i.Ingredient!.Name, i.Quantity, i.Unit, i.Optional))
                .ToList(),
            recipe.Tips,
            recipe.Pairing
        );
}
