using FluentAssertions;
using Moq;
using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Services;

public class IngredientsServiceTests
{
    private readonly Mock<IIngredientRepository> _repository = new();
    private readonly IngredientsService _service;

    public IngredientsServiceTests()
    {
        _service = new IngredientsService(_repository.Object);
    }

    [Fact]
    public async Task DeleteAsync_WhenUnused_DeletesIngredient()
    {
        _repository.Setup(r => r.CountRecipeUsagesAsync(1)).ReturnsAsync(0);
        _repository.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        await _service.DeleteAsync(1);

        _repository.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUsedInRecipes_ThrowsConflictWithoutDeleting()
    {
        _repository.Setup(r => r.CountRecipeUsagesAsync(1)).ReturnsAsync(3);

        var act = () => _service.DeleteAsync(1);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.Message.Should().Contain("3 recipes");
        _repository.Verify(r => r.DeleteAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenUsedInExactlyOneRecipe_UsesSingularWording()
    {
        _repository.Setup(r => r.CountRecipeUsagesAsync(1)).ReturnsAsync(1);

        var act = () => _service.DeleteAsync(1);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.Message.Should().Contain("1 recipe").And.NotContain("1 recipes");
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ThrowsNotFound()
    {
        _repository.Setup(r => r.CountRecipeUsagesAsync(42)).ReturnsAsync(0);
        _repository.Setup(r => r.DeleteAsync(42)).ReturnsAsync(false);

        var act = () => _service.DeleteAsync(42);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenFound_ReturnsUpdatedResponse()
    {
        _repository
            .Setup(r => r.UpdateAsync(1, It.IsAny<Action<Ingredient>>()))
            .ReturnsAsync((long _, Action<Ingredient> apply) =>
            {
                var ingredient = new Ingredient { Id = 1, Name = "Old", DefaultUnit = "g" };
                apply(ingredient);
                return ingredient;
            });

        var result = await _service.UpdateAsync(1, new UpdateIngredientRequest("Bread Flour", "baking_flour", "g"));

        result.Name.Should().Be("Bread Flour");
        result.Category.Should().Be("baking_flour");
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ThrowsNotFound()
    {
        _repository
            .Setup(r => r.UpdateAsync(42, It.IsAny<Action<Ingredient>>()))
            .ReturnsAsync((Ingredient?)null);

        var act = () => _service.UpdateAsync(42, new UpdateIngredientRequest("Flour", "baking_flour", "g"));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
