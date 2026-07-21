using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PantryService.Controllers;
using PantryService.Dtos;
using PantryService.Services;
using Xunit;

namespace PantryService.Tests.Controllers;

public class PantryControllerTests
{
    private readonly Mock<IPantryItemsService> _service = new();
    private readonly PantryController _controller;

    public PantryControllerTests()
    {
        _controller = new PantryController(_service.Object);
    }

    [Fact]
    public async Task GetForUser_ReturnsOkWithItems()
    {
        var items = new List<PantryItemResponse>
        {
            new(1, 42, 7, "Flour", 500, "g", null, DateTimeOffset.UtcNow)
        };
        _service.Setup(s => s.GetForUserAsync(42)).ReturnsAsync(items);

        var result = await _controller.GetForUser(42);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        _service.Setup(s => s.DeleteAsync(42, 1)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(42, 1);

        result.Should().BeOfType<NoContentResult>();
        _service.Verify(s => s.DeleteAsync(42, 1), Times.Once);
    }

    [Fact]
    public async Task GetMissingIngredients_ReturnsOkWithDiff()
    {
        var response = new MissingIngredientsResponse(
            RecipeId: 9,
            RecipeTitle: "Bread",
            MissingIngredients: [new MissingIngredientResponse(7, "Flour", 500, 100, "g")]
        );
        _service.Setup(s => s.GetMissingIngredientsAsync(42, 9)).ReturnsAsync(response);

        var result = await _controller.GetMissingIngredients(42, 9);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }
}
