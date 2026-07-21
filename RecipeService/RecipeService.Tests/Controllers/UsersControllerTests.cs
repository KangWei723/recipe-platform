using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RecipeService.Controllers;
using RecipeService.Dtos;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUsersService> _service = new();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_service.Object);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithUser()
    {
        var expected = new UserResponse(1, "ada@example.com", "Ada Lovelace", DateTimeOffset.UtcNow);
        _service.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(expected);

        var result = await _controller.GetById(1);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var request = new CreateUserRequest("ada@example.com", "Ada Lovelace");
        var created = new UserResponse(1, request.Email, request.Name, DateTimeOffset.UtcNow);
        _service.Setup(s => s.CreateAsync(request)).ReturnsAsync(created);

        var result = await _controller.Create(request);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(created);
        createdResult.ActionName.Should().Be(nameof(UsersController.GetById));
    }
}
