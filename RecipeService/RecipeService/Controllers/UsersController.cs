using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(IUsersService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll() =>
        Ok(await service.GetAllAsync());

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> GetById(long id) =>
        Ok(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // Resolves the caller's own user record from their validated token, provisioning one
    // just-in-time on first sign-in. This is how every other service learns "who am I" as a
    // numeric id, since Auth0 only gives them a string `sub`.
    [HttpPost("me")]
    public async Task<ActionResult<UserResponse>> Me() =>
        Ok(await service.ResolveCurrentUserAsync(User));
}
