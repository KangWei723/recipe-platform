using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RecipeService.Controllers;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Controllers;

public class RecipesControllerTests
{
    private readonly Mock<IRecipesService> _service = new();
    private readonly RecipesController _controller;

    public RecipesControllerTests()
    {
        _controller = new RecipesController(_service.Object);
    }

    [Fact]
    public async Task GetById_WhenMissing_PropagatesNotFoundException()
    {
        _service.Setup(s => s.GetByIdAsync(42)).ThrowsAsync(new NotFoundException("Recipe 42 not found"));

        var act = () => _controller.GetById(42);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithDetail()
    {
        var request = new CreateRecipeRequest(
            AuthorId: 1,
            Title: "Weeknight Pasta",
            Description: "Fast and simple",
            Servings: 2,
            PrepTimeMin: 10,
            CookTimeMin: 15,
            ImageUrl: null,
            Steps: [new CreateRecipeStepRequest(1, "Boil water", 300)],
            Ingredients: [new CreateRecipeIngredientRequest(5, 200, "g", false)]
        );

        var detail = new RecipeDetailResponse(
            Id: 10,
            AuthorId: 1,
            Title: request.Title,
            Description: request.Description,
            Servings: request.Servings,
            PrepTimeMin: request.PrepTimeMin,
            CookTimeMin: request.CookTimeMin,
            ImageUrl: request.ImageUrl,
            CreatedAt: DateTimeOffset.UtcNow,
            Steps: [new RecipeStepResponse(1, 1, "Boil water", 300)],
            Ingredients: [new RecipeIngredientResponse(1, 5, "Pasta", 200, "g", false)]
        );

        _service.Setup(s => s.CreateAsync(request)).ReturnsAsync(detail);

        var result = await _controller.Create(request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(detail);
    }
}
