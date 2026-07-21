using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RecipeService.Data;
using RecipeService.Domain;
using RecipeService.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace RecipeService.Tests.Repositories;

// Requires a Docker daemon reachable from the test host; Testcontainers spins up
// a throwaway Postgres per run instead of relying on a shared/mocked database.
public class RecipeRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private RecipeDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new RecipeDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_PersistsRecipeWithStepsAndIngredients()
    {
        var user = new User { Email = "chef@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour", DefaultUnit = "g" };
        _context.Users.Add(user);
        _context.Ingredients.Add(flour);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread",
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = { new RecipeStep { StepNumber = 1, Instruction = "Mix" } },
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        };

        var created = await repository.AddAsync(recipe);
        var fetched = await repository.GetByIdAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Steps.Should().ContainSingle(s => s.Instruction == "Mix");
        fetched.Ingredients.Should().ContainSingle(i => i.Ingredient!.Name == "Flour");
    }
}
