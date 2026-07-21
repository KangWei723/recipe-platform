using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
using RecipeService.Repositories;

namespace RecipeService.Services;

public class UsersService(IUserRepository repository) : IUsersService
{
    public async Task<UserResponse> GetByIdAsync(long id)
    {
        var user = await repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"User {id} not found");
        return ToResponse(user);
    }

    public async Task<List<UserResponse>> GetAllAsync()
    {
        var users = await repository.GetAllAsync();
        return users.Select(ToResponse).ToList();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        var user = new User
        {
            Email = request.Email,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await repository.AddAsync(user);
        return ToResponse(created);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.Name, user.CreatedAt);
}
