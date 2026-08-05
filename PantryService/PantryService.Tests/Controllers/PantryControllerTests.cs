using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PantryService.Auth;
using PantryService.Controllers;
using PantryService.Dtos;
using PantryService.Services;
using Xunit;

namespace PantryService.Tests.Controllers;

public class PantryControllerTests
{
    private readonly Mock<IPantryItemsService> _service = new();
    private readonly Mock<ICurrentUserResolver> _currentUser = new();
    private readonly PantryController _controller;

    public PantryControllerTests()
    {
        _controller = new PantryController(_service.Object, _currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "auth0|test-user")], "TestAuth"))
                }
            }
        };
        _currentUser.Setup(r => r.ResolveUserIdAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
    }

    [Fact]
    public async Task GetForUser_ReturnsOkWithItems()
    {
        var items = new List<PantryItemResponse>
        {
            new(1, 42, 7, "Flour", 500, "g", null, DateTimeOffset.UtcNow)
        };
        _service.Setup(s => s.GetForUserAsync(42)).ReturnsAsync(items);

        var result = await _controller.GetForUser(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        _service.Setup(s => s.DeleteAsync(42, 1)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(1, CancellationToken.None);

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

        var result = await _controller.GetMissingIngredients(9, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetForUser_DerivesUserIdFromTokenNotClient()
    {
        // The whole point of the fix: nothing in the controller call supplies a userId --
        // it can only come from whatever ICurrentUserResolver resolves from the validated
        // principal, so a caller can no longer request another user's pantry via the URL.
        _currentUser.Setup(r => r.ResolveUserIdAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        _service.Setup(s => s.GetForUserAsync(99)).ReturnsAsync([]);

        await _controller.GetForUser(CancellationToken.None);

        _service.Verify(s => s.GetForUserAsync(99), Times.Once);
        _service.Verify(s => s.GetForUserAsync(It.Is<long>(id => id != 99)), Times.Never);
    }
}
