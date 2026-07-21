using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IUsersService
{
    Task<UserResponse> GetByIdAsync(long id);
    Task<List<UserResponse>> GetAllAsync();
    Task<UserResponse> CreateAsync(CreateUserRequest request);
}
