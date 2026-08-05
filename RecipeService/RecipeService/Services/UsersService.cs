using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<UserResponse> ResolveCurrentUserAsync(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("Validated token is missing a 'sub' claim");

        var existing = await repository.GetByAuthSubAsync(sub);
        if (existing is not null)
        {
            return ToResponse(existing);
        }

        // Auth0 access tokens (as opposed to ID tokens) don't carry profile claims like
        // email/name unless a Post-Login Action explicitly adds them -- fall back to
        // sub-derived placeholders rather than failing provisioning outright when they're
        // absent. Email must stay unique/non-null per the schema.
        var email = principal.FindFirst("email")?.Value ?? $"{sub}@users.recipemate.invalid";
        var name = principal.FindFirst("name")?.Value ?? principal.FindFirst("nickname")?.Value ?? "New User";

        var user = new User
        {
            AuthSub = sub,
            Email = email,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var created = await repository.AddAsync(user);
            return ToResponse(created);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost a race with a concurrent first-login request for the same sub (e.g. two
            // tabs resolving identity right after sign-in) -- the other request's insert won,
            // so just return what it created instead of surfacing a spurious 500.
            var winner = await repository.GetByAuthSubAsync(sub)
                ?? throw new InvalidOperationException($"Unique violation provisioning user for sub {sub}, but no row found afterward");
            return ToResponse(winner);
        }
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.Name, user.CreatedAt);
}
