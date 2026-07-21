using FluentAssertions;
using Moq;
using PantryService.Client;
using PantryService.Domain;
using PantryService.Dtos;
using PantryService.Exceptions;
using PantryService.Repositories;
using PantryService.Services;
using Xunit;

namespace PantryService.Tests.Services;

public class PantryItemsServiceTests
{
    private readonly Mock<IPantryItemRepository> _repository = new();
    private readonly Mock<IRecipeServiceClient> _recipeClient = new();
    private readonly PantryItemsService _service;

    public PantryItemsServiceTests()
    {
        _service = new PantryItemsService(_repository.Object, _recipeClient.Object);
    }

    [Fact]
    public async Task GetMissingIngredientsAsync_WhenRecipeDoesNotExist_ThrowsNotFound()
    {
        _recipeClient.Setup(c => c.GetRecipeAsync(99, default)).ReturnsAsync((RecipeDetailDto?)null);

        var act = () => _service.GetMissingIngredientsAsync(userId: 1, recipeId: 99);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetMissingIngredientsAsync_ReturnsOnlyInsufficientNonOptionalIngredients()
    {
        var recipe = new RecipeDetailDto(
            Id: 9,
            AuthorId: 1,
            Title: "Bread",
            Ingredients:
            [
                new RecipeIngredientDto(1, IngredientId: 7, IngredientName: "Flour", Quantity: 500, Unit: "g", Optional: false),
                new RecipeIngredientDto(2, IngredientId: 8, IngredientName: "Yeast", Quantity: 10, Unit: "g", Optional: false),
                new RecipeIngredientDto(3, IngredientId: 9, IngredientName: "Sugar", Quantity: 20, Unit: "g", Optional: true)
            ]
        );
        _recipeClient.Setup(c => c.GetRecipeAsync(9, default)).ReturnsAsync(recipe);

        _repository.Setup(r => r.GetForUserAsync(1)).ReturnsAsync(
        [
            new PantryItem { Id = 1, UserId = 1, IngredientId = 7, Quantity = 100, Unit = "g", UpdatedAt = DateTimeOffset.UtcNow }
            // No pantry entry at all for Yeast (id 8) or Sugar (id 9).
        ]);

        var result = await _service.GetMissingIngredientsAsync(userId: 1, recipeId: 9);

        result.MissingIngredients.Should().HaveCount(2);
        result.MissingIngredients.Should().ContainSingle(m => m.IngredientId == 7 && m.AvailableQuantity == 100);
        result.MissingIngredients.Should().ContainSingle(m => m.IngredientId == 8 && m.AvailableQuantity == 0);
        result.MissingIngredients.Should().NotContain(m => m.IngredientId == 9);
    }

    [Fact]
    public async Task UpsertAsync_WhenIngredientUnknown_ThrowsValidation()
    {
        _recipeClient.Setup(c => c.GetIngredientAsync(123, default)).ReturnsAsync((IngredientDto?)null);

        var act = () => _service.UpsertAsync(1, new UpsertPantryItemRequest(123, 100, "g", null));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
