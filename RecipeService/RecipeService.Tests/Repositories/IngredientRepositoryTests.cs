using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeService.Data;
using RecipeService.Domain;
using RecipeService.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace RecipeService.Tests.Repositories;

// Requires a Docker daemon reachable from the test host; Testcontainers spins up
// a throwaway Postgres per run instead of relying on a shared/mocked database.
public class IngredientRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private RecipeDbContext _context = null!;
    private DbContextOptions<RecipeDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new RecipeDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task CountRecipeUsagesAsync_ReturnsNumberOfRecipeIngredientRows()
    {
        var user = new User { Email = "chef@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour", DefaultUnit = "g", Category = "other" };
        _context.Users.Add(user);
        _context.Ingredients.Add(flour);
        await _context.SaveChangesAsync();

        _context.Recipes.Add(new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread",
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        });
        _context.Recipes.Add(new Recipe
        {
            AuthorId = user.Id,
            Title = "Pancakes",
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 200, Unit = "g" } }
        });
        await _context.SaveChangesAsync();

        var repository = new IngredientRepository(_context);
        var count = await repository.CountRecipeUsagesAsync(flour.Id);

        count.Should().Be(2);
    }

    [Fact]
    public async Task CountAllRecipeUsagesAsync_ReturnsCountsGroupedByIngredient()
    {
        var user = new User { Email = "chef3@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour3", DefaultUnit = "g", Category = "other" };
        var sugar = new Ingredient { Name = "Sugar3", DefaultUnit = "g", Category = "other" };
        var unused = new Ingredient { Name = "Unused3", DefaultUnit = "g", Category = "other" };
        _context.Users.Add(user);
        _context.Ingredients.AddRange(flour, sugar, unused);
        await _context.SaveChangesAsync();

        _context.Recipes.Add(new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread",
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients =
            {
                new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" },
                new RecipeIngredient { IngredientId = sugar.Id, Quantity = 10, Unit = "g" }
            }
        });
        _context.Recipes.Add(new Recipe
        {
            AuthorId = user.Id,
            Title = "Pancakes",
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 200, Unit = "g" } }
        });
        await _context.SaveChangesAsync();

        var repository = new IngredientRepository(_context);
        var counts = await repository.CountAllRecipeUsagesAsync();

        counts.Should().Contain(flour.Id, 2).And.Contain(sugar.Id, 1);
        counts.Should().NotContainKey(unused.Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenReferencedByRecipeIngredient_ThrowsForeignKeyViolation()
    {
        var user = new User { Email = "chef2@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour2", DefaultUnit = "g", Category = "other" };
        _context.Users.Add(user);
        _context.Ingredients.Add(flour);
        await _context.SaveChangesAsync();

        _context.Recipes.Add(new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread",
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        });
        await _context.SaveChangesAsync();

        // A fresh DbContext, not _context, matches the real per-request scoped DbContext this
        // repository actually runs under -- IngredientRepository.DeleteAsync only ever loads the
        // bare Ingredient row (Ingredient has no navigation collection back to
        // RecipeIngredient), so nothing is client-side tracked to sever. Reusing _context here
        // would instead make EF Core's change tracker throw InvalidOperationException itself
        // (it already has the RecipeIngredient loaded from the setup above), never reaching
        // Postgres, which isn't what production does.
        await using var freshContext = new RecipeDbContext(_options);
        var repository = new IngredientRepository(freshContext);
        var act = () => repository.DeleteAsync(flour.Id);

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task DeleteAsync_WhenUnreferenced_Succeeds()
    {
        var ingredient = new Ingredient { Name = "Unused", DefaultUnit = "g", Category = "other" };
        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();

        var repository = new IngredientRepository(_context);
        var deleted = await repository.DeleteAsync(ingredient.Id);

        deleted.Should().BeTrue();
        (await _context.Ingredients.FindAsync(ingredient.Id)).Should().BeNull();
    }
}
