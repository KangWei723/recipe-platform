using System.Security.Claims;
using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IUsersService
{
    Task<UserResponse> GetByIdAsync(long id);
    Task<List<UserResponse>> GetAllAsync();
    Task<UserResponse> CreateAsync(CreateUserRequest request);

    /// <summary>
    /// Looks up the user record for the validated token's "sub" claim, creating one
    /// just-in-time on first sign-in if none exists yet.
    /// </summary>
    Task<UserResponse> ResolveCurrentUserAsync(ClaimsPrincipal principal);
}
