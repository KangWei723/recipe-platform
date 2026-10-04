using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
    private readonly Mock<IUsersService> _usersService = new();
    private readonly RecipesController _controller;

    public RecipesControllerTests()
    {
        _controller = new RecipesController(_service.Object, _usersService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "auth0|test-user")], "TestAuth"))
                }
            }
        };
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
            Title: "Weeknight Pasta",
            Description: "Fast and simple",
            Servings: 2,
            PrepTimeMin: 10,
            CookTimeMin: 15,
            ImageUrl: "https://cdn.example.com/pasta.jpg",
            Steps: [new CreateRecipeStepRequest(1, "Boil water", 300, "https://cdn.example.com/boil.jpg")],
            Ingredients: [new CreateRecipeIngredientRequest(5, 200, "g", false)],
            Tips: ["Salt the water generously"],
            Pairing: "Serve with a light white wine"
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
            Steps: [new RecipeStepResponse(1, 1, "Boil water", 300, "https://cdn.example.com/boil.jpg")],
            Ingredients: [new RecipeIngredientResponse(1, 5, "Pasta", 200, "g", false)],
            Tips: request.Tips,
            Pairing: request.Pairing
        );

        _usersService.Setup(s => s.ResolveCurrentUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new UserResponse(1, "author@example.com", "Author", DateTimeOffset.UtcNow));
        _service.Setup(s => s.CreateAsync(request, 1)).ReturnsAsync(detail);

        var result = await _controller.Create(request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(detail);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedDetail()
    {
        var request = new UpdateRecipeRequest(
            Title: "Updated Pasta",
            Description: "Now with garlic",
            Servings: 4,
            PrepTimeMin: 15,
            CookTimeMin: 20,
            ImageUrl: "https://cdn.example.com/pasta-v2.jpg",
            Steps: [new CreateRecipeStepRequest(1, "Boil water", 300, "https://cdn.example.com/boil.jpg")],
            Ingredients: [new CreateRecipeIngredientRequest(5, 300, "g", false)],
            Tips: ["Add garlic early"],
            Pairing: "Serve with garlic bread"
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
            Steps: [new RecipeStepResponse(1, 1, "Boil water", 300, "https://cdn.example.com/boil.jpg")],
            Ingredients: [new RecipeIngredientResponse(1, 5, "Pasta", 300, "g", false)],
            Tips: request.Tips,
            Pairing: request.Pairing
        );

        _service.Setup(s => s.UpdateAsync(10, request)).ReturnsAsync(detail);

        var result = await _controller.Update(10, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(detail);
    }

    [Fact]
    public async Task Update_WhenMissing_PropagatesNotFoundException()
    {
        var request = new UpdateRecipeRequest("Title", null, null, null, null, null, [], []);
        _service.Setup(s => s.UpdateAsync(42, request)).ThrowsAsync(new NotFoundException("Recipe 42 not found"));

        var act = () => _controller.Update(42, request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        _service.Setup(s => s.DeleteAsync(10)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(10);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WhenMissing_PropagatesNotFoundException()
    {
        _service.Setup(s => s.DeleteAsync(42)).ThrowsAsync(new NotFoundException("Recipe 42 not found"));

        var act = () => _controller.Delete(42);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
