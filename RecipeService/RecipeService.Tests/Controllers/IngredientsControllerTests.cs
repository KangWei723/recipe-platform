using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RecipeService.Controllers;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Controllers;

public class IngredientsControllerTests
{
    private readonly Mock<IIngredientsService> _service = new();
    private readonly IngredientsController _controller;

    public IngredientsControllerTests()
    {
        _controller = new IngredientsController(_service.Object);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithIngredient()
    {
        var request = new CreateIngredientRequest("Flour", "baking_flour", "g");
        var created = new IngredientResponse(1, request.Name, request.Category, request.DefaultUnit, 0);
        _service.Setup(s => s.CreateAsync(request)).ReturnsAsync(created);

        var result = await _controller.Create(request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(created);
        createdResult.ActionName.Should().Be(nameof(IngredientsController.GetById));
    }

    [Fact]
    public async Task Update_ReturnsOkWithUpdatedIngredient()
    {
        var request = new UpdateIngredientRequest("Bread Flour", "baking_flour", "g");
        var updated = new IngredientResponse(1, request.Name, request.Category, request.DefaultUnit, 3);
        _service.Setup(s => s.UpdateAsync(1, request)).ReturnsAsync(updated);

        var result = await _controller.Update(1, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task Update_WhenMissing_PropagatesNotFoundException()
    {
        var request = new UpdateIngredientRequest("Flour", "baking_flour", "g");
        _service.Setup(s => s.UpdateAsync(42, request)).ThrowsAsync(new NotFoundException("Ingredient 42 not found"));

        var act = () => _controller.Update(42, request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        _service.Setup(s => s.DeleteAsync(1)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(1);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WhenUsedInRecipes_PropagatesConflictException()
    {
        _service.Setup(s => s.DeleteAsync(1))
            .ThrowsAsync(new ConflictException("Cannot delete ingredient: used in 3 recipes"));

        var act = () => _controller.Delete(1);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
