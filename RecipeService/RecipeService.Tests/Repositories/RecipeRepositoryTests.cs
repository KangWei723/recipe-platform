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

    [Fact]
    public async Task UpdateAsync_ReplacesStepsAndIngredients_ReusingSameStepNumbers()
    {
        var user = new User { Email = "chef2@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour2", DefaultUnit = "g" };
        var yeast = new Ingredient { Name = "Yeast2", DefaultUnit = "g" };
        _context.Users.Add(user);
        _context.Ingredients.AddRange(flour, yeast);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = await repository.AddAsync(new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread v1",
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = { new RecipeStep { StepNumber = 1, Instruction = "Mix" } },
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        });

        // Reuses step_number 1 -- exercises the UNIQUE(recipe_id, step_number) constraint that
        // motivated the two-SaveChangesAsync-calls design (clear+save before add+save).
        var updated = await repository.UpdateAsync(
            recipe.Id,
            title: "Bread v2",
            description: "Updated",
            servings: 4,
            prepTimeMin: 10,
            cookTimeMin: 30,
            imageUrl: null,
            steps: [new RecipeStep { StepNumber = 1, Instruction = "Knead" }],
            ingredients: [new RecipeIngredient { IngredientId = yeast.Id, Quantity = 10, Unit = "g" }]
        );

        updated.Should().NotBeNull();
        var fetched = await repository.GetByIdAsync(recipe.Id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Bread v2");
        fetched.Steps.Should().ContainSingle(s => s.Instruction == "Knead" && s.StepNumber == 1);
        fetched.Ingredients.Should().ContainSingle(i => i.Ingredient!.Name == "Yeast2");
        fetched.Ingredients.Should().NotContain(i => i.Ingredient!.Name == "Flour2");
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ReturnsNull()
    {
        var repository = new RecipeRepository(_context);

        var result = await repository.UpdateAsync(
            999999, "Title", null, null, null, null, null, [], []);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecipeAndCascadesStepsAndIngredients()
    {
        var user = new User { Email = "chef3@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour3", DefaultUnit = "g" };
        _context.Users.Add(user);
        _context.Ingredients.Add(flour);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = await repository.AddAsync(new Recipe
        {
            AuthorId = user.Id,
            Title = "To Delete",
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = { new RecipeStep { StepNumber = 1, Instruction = "Mix" } },
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        });

        var deleted = await repository.DeleteAsync(recipe.Id);

        deleted.Should().BeTrue();
        (await repository.GetByIdAsync(recipe.Id)).Should().BeNull();
        (await _context.RecipeSteps.AnyAsync(s => s.RecipeId == recipe.Id)).Should().BeFalse();
        (await _context.RecipeIngredients.AnyAsync(i => i.RecipeId == recipe.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ReturnsFalse()
    {
        var repository = new RecipeRepository(_context);

        var deleted = await repository.DeleteAsync(999999);

        deleted.Should().BeFalse();
    }
}
