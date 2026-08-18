using Auth;
using Gateway.Client;
using Gateway.Models;
using HotChocolate;
using HotChocolate.Authorization;

namespace Gateway.GraphQL;

public class Mutation
{
    // No userId argument, same reason as Query.PantryItemsAsync: pantry-service resolves the
    // caller from the forwarded bearer token, not a client-supplied value.
    public async Task<PantryItem> UpsertPantryItemAsync(
        long ingredientId,
        decimal quantity,
        string unit,
        DateOnly? expiryDate,
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        var dto = await pantryClient.UpsertAsync(
            new UpsertPantryItemDto(ingredientId, quantity, unit, expiryDate),
            cancellationToken);

        return new PantryItem
        {
            Id = dto.Id,
            UserId = dto.UserId,
            IngredientId = dto.IngredientId,
            IngredientName = dto.IngredientName,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            ExpiryDate = dto.ExpiryDate,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public async Task<bool> RemovePantryItemAsync(
        long itemId,
        [Service] IPantryServiceClient pantryClient,
        CancellationToken cancellationToken)
    {
        await pantryClient.DeleteAsync(itemId, cancellationToken);
        return true;
    }

    // No author argument, same reasoning as the pantry mutations above: recipe-service derives
    // the author from the caller's own forwarded bearer token (see IRecipeServiceClient.CreateAsync),
    // not from anything a client could supply -- this is the same fix already applied to
    // CreateRecipeRequest.AuthorId on the REST side.
    //
    // [Authorize] here is a UX shortcut, not the real boundary -- recipe-service enforces the
    // same AuthorizationPolicies.AdminOnly policy on the forwarded request regardless. Applied to
    // every recipe/ingredient write mutation below for the same reason.
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<RecipeSummary> CreateRecipeAsync(
        string title,
        string? description,
        int? servings,
        int? prepTimeMin,
        int? cookTimeMin,
        List<CreateRecipeStepInput> steps,
        List<CreateRecipeIngredientInput> ingredients,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var dto = await recipeClient.CreateAsync(
            new CreateRecipeDto(
                title,
                description,
                servings,
                prepTimeMin,
                cookTimeMin,
                ImageUrl: null,
                steps
                    .Select(s => new CreateRecipeStepDto(s.StepNumber, s.Instruction, s.TimerSeconds))
                    .ToList(),
                ingredients
                    .Select(i => new CreateRecipeIngredientDto(i.IngredientId, i.Quantity, i.Unit, i.Optional))
                    .ToList()),
            cancellationToken);

        return ToRecipeSummary(dto);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<RecipeSummary> UpdateRecipeAsync(
        long recipeId,
        string title,
        string? description,
        int? servings,
        int? prepTimeMin,
        int? cookTimeMin,
        List<CreateRecipeStepInput> steps,
        List<CreateRecipeIngredientInput> ingredients,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var dto = await recipeClient.UpdateAsync(
            recipeId,
            new UpdateRecipeDto(
                title,
                description,
                servings,
                prepTimeMin,
                cookTimeMin,
                ImageUrl: null,
                steps
                    .Select(s => new CreateRecipeStepDto(s.StepNumber, s.Instruction, s.TimerSeconds))
                    .ToList(),
                ingredients
                    .Select(i => new CreateRecipeIngredientDto(i.IngredientId, i.Quantity, i.Unit, i.Optional))
                    .ToList()),
            cancellationToken);

        return ToRecipeSummary(dto);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<bool> DeleteRecipeAsync(
        long recipeId,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        await recipeClient.DeleteAsync(recipeId, cancellationToken);
        return true;
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<Ingredient> CreateIngredientAsync(
        string name,
        string? category,
        string defaultUnit,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var dto = await recipeClient.CreateIngredientAsync(
            new CreateIngredientDto(name, category, defaultUnit), cancellationToken);
        return ToIngredient(dto);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<Ingredient> UpdateIngredientAsync(
        long ingredientId,
        string name,
        string? category,
        string defaultUnit,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        var dto = await recipeClient.UpdateIngredientAsync(
            ingredientId, new UpdateIngredientDto(name, category, defaultUnit), cancellationToken);
        return ToIngredient(dto);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<bool> DeleteIngredientAsync(
        long ingredientId,
        [Service] IRecipeServiceClient recipeClient,
        CancellationToken cancellationToken)
    {
        await recipeClient.DeleteIngredientAsync(ingredientId, cancellationToken);
        return true;
    }

    private static RecipeSummary ToRecipeSummary(RecipeDetailDto dto) =>
        new()
        {
            Id = dto.Id,
            AuthorId = dto.AuthorId,
            Title = dto.Title,
            Description = dto.Description,
            Servings = dto.Servings,
            PrepTimeMin = dto.PrepTimeMin,
            CookTimeMin = dto.CookTimeMin,
            ImageUrl = dto.ImageUrl,
            CreatedAt = dto.CreatedAt
        };

    private static Ingredient ToIngredient(IngredientDto dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name,
            Category = dto.Category,
            DefaultUnit = dto.DefaultUnit
        };
}
