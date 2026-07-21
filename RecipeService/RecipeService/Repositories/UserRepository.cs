using Microsoft.EntityFrameworkCore;
using RecipeService.Data;
using RecipeService.Domain;

namespace RecipeService.Repositories;

public class UserRepository(RecipeDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<List<User>> GetAllAsync() =>
        context.Users.OrderBy(u => u.Id).ToListAsync();

    public async Task<User> AddAsync(User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
