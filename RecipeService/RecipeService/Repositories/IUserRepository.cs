using RecipeService.Domain;

namespace RecipeService.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id);
    Task<User?> GetByAuthSubAsync(string authSub);
    Task<List<User>> GetAllAsync();
    Task<User> AddAsync(User user);
}
