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
        var flour = new Ingredient { Name = "Flour", DefaultUnit = "g", Category = "other" };
        _context.Users.Add(user);
        _context.Ingredients.Add(flour);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread",
            CreatedAt = DateTimeOffset.UtcNow,
            ImageUrl = "https://cdn.example.com/bread.jpg",
            Tips = ["Let the dough rest", "Use room-temperature water"],
            Pairing = "Serve with olive oil",
            Steps = { new RecipeStep { StepNumber = 1, Instruction = "Mix", ImageUrl = "https://cdn.example.com/mix.jpg" } },
            Ingredients = { new RecipeIngredient { IngredientId = flour.Id, Quantity = 500, Unit = "g" } }
        };

        var created = await repository.AddAsync(recipe);
        var fetched = await repository.GetByIdAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.ImageUrl.Should().Be("https://cdn.example.com/bread.jpg");
        fetched.Tips.Should().Equal("Let the dough rest", "Use room-temperature water");
        fetched.Pairing.Should().Be("Serve with olive oil");
        fetched.Steps.Should().ContainSingle(s => s.Instruction == "Mix" && s.ImageUrl == "https://cdn.example.com/mix.jpg");
        fetched.Ingredients.Should().ContainSingle(i => i.Ingredient!.Name == "Flour");
    }

    [Fact]
    public async Task UpdateAsync_ClearsImageUrl_WhenSetToNull()
    {
        // Proves the "remove image" direction, not just "set image" -- the full-replace update
        // path already does this unconditionally by construction (recipe.ImageUrl = imageUrl and
        // each new RecipeStep's ImageUrl are plain assignments, no "only if non-null" special
        // case), but nothing previously exercised going from an image set to explicitly cleared.
        var user = new User { Email = "chef6@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = await repository.AddAsync(new Recipe
        {
            AuthorId = user.Id,
            Title = "Bread With Photos",
            CreatedAt = DateTimeOffset.UtcNow,
            ImageUrl = "https://cdn.example.com/bread.jpg",
            Steps = { new RecipeStep { StepNumber = 1, Instruction = "Mix", ImageUrl = "https://cdn.example.com/mix.jpg" } }
        });

        await repository.UpdateAsync(
            recipe.Id,
            title: recipe.Title,
            description: null,
            servings: null,
            prepTimeMin: null,
            cookTimeMin: null,
            imageUrl: null,
            steps: [new RecipeStep { StepNumber = 1, Instruction = "Mix", ImageUrl = null }],
            ingredients: [],
            tips: [],
            pairing: null
        );

        var fetched = await repository.GetByIdAsync(recipe.Id);
        fetched.Should().NotBeNull();
        fetched!.ImageUrl.Should().BeNull();
        fetched.Steps.Should().ContainSingle(s => s.Instruction == "Mix" && s.ImageUrl == null);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesStepsAndIngredients_ReusingSameStepNumbers()
    {
        var user = new User { Email = "chef2@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour2", DefaultUnit = "g", Category = "other" };
        var yeast = new Ingredient { Name = "Yeast2", DefaultUnit = "g", Category = "other" };
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
            steps: [new RecipeStep { StepNumber = 1, Instruction = "Knead", ImageUrl = "https://cdn.example.com/knead.jpg" }],
            ingredients: [new RecipeIngredient { IngredientId = yeast.Id, Quantity = 10, Unit = "g" }],
            tips: ["Knead until elastic"],
            pairing: "Serve with butter"
        );

        updated.Should().NotBeNull();
        var fetched = await repository.GetByIdAsync(recipe.Id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Bread v2");
        fetched.Steps.Should().ContainSingle(s =>
            s.Instruction == "Knead" && s.StepNumber == 1 && s.ImageUrl == "https://cdn.example.com/knead.jpg");
        fetched.Ingredients.Should().ContainSingle(i => i.Ingredient!.Name == "Yeast2");
        fetched.Ingredients.Should().NotContain(i => i.Ingredient!.Name == "Flour2");
        fetched.Tips.Should().Equal("Knead until elastic");
        fetched.Pairing.Should().Be("Serve with butter");
    }

    [Fact]
    public async Task UpdateAsync_StepImageUrlMovesWithStepWhenReordered()
    {
        var user = new User { Email = "chef4@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var repository = new RecipeRepository(_context);
        var recipe = await repository.AddAsync(new Recipe
        {
            AuthorId = user.Id,
            Title = "Two Step Recipe",
            CreatedAt = DateTimeOffset.UtcNow,
            Steps =
            {
                new RecipeStep { StepNumber = 1, Instruction = "First", ImageUrl = "https://cdn.example.com/first.jpg" },
                new RecipeStep { StepNumber = 2, Instruction = "Second", ImageUrl = "https://cdn.example.com/second.jpg" }
            }
        });

        // Submits the same two steps with their order swapped -- step numbers are reassigned by
        // position (same as the web-client form does on drag-reorder), so this exercises that
        // each step's own ImageUrl travels with its content rather than staying pinned to a
        // step_number.
        await repository.UpdateAsync(
            recipe.Id,
            title: recipe.Title,
            description: null,
            servings: null,
            prepTimeMin: null,
            cookTimeMin: null,
            imageUrl: null,
            steps:
            [
                new RecipeStep { StepNumber = 1, Instruction = "Second", ImageUrl = "https://cdn.example.com/second.jpg" },
                new RecipeStep { StepNumber = 2, Instruction = "First", ImageUrl = "https://cdn.example.com/first.jpg" }
            ],
            ingredients: [],
            tips: [],
            pairing: null
        );

        var fetched = await repository.GetByIdAsync(recipe.Id);
        fetched.Should().NotBeNull();
        var orderedSteps = fetched!.Steps.OrderBy(s => s.StepNumber).ToList();
        orderedSteps[0].Instruction.Should().Be("Second");
        orderedSteps[0].ImageUrl.Should().Be("https://cdn.example.com/second.jpg");
        orderedSteps[1].Instruction.Should().Be("First");
        orderedSteps[1].ImageUrl.Should().Be("https://cdn.example.com/first.jpg");
    }

    [Fact]
    public async Task GetByIdAsync_WhenTipsColumnWasLeftAtItsDefault_LoadsWithEmptyTips()
    {
        // Simulates a row this EF model never wrote -- a raw SQL insert (standing in for a
        // pre-migration row, or any future insert that doesn't go through RecipesService) that
        // omits the tips column entirely, relying on the column's own DB-level
        // DEFAULT ARRAY[]::text[] rather than the C# domain class's `= new()` default. Every
        // other test in this file creates rows through the EF model, which always supplies a
        // non-null Tips, so none of them would have caught Recipe.Tips (non-nullable List<string>)
        // being backed by a column that could still produce NULL -- this is the case that was
        // missed and caused "Column 'tips' is null" against real pre-migration Neon rows.
        var user = new User { Email = "chef5@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _context.Database.OpenConnectionAsync();
        var connection = _context.Database.GetDbConnection();
        long insertedId;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO recipes (author_id, title, created_at) VALUES (@authorId, @title, now()) RETURNING id";

            var authorIdParam = command.CreateParameter();
            authorIdParam.ParameterName = "authorId";
            authorIdParam.Value = user.Id;
            command.Parameters.Add(authorIdParam);

            var titleParam = command.CreateParameter();
            titleParam.ParameterName = "title";
            titleParam.Value = "Legacy Recipe";
            command.Parameters.Add(titleParam);

            insertedId = (long)(await command.ExecuteScalarAsync())!;
        }

        var repository = new RecipeRepository(_context);
        var fetched = await repository.GetByIdAsync(insertedId);

        fetched.Should().NotBeNull();
        fetched!.Tips.Should().NotBeNull();
        fetched.Tips.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ReturnsNull()
    {
        var repository = new RecipeRepository(_context);

        var result = await repository.UpdateAsync(
            999999, "Title", null, null, null, null, null, [], [], [], null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecipeAndCascadesStepsAndIngredients()
    {
        var user = new User { Email = "chef3@example.com", Name = "Chef", CreatedAt = DateTimeOffset.UtcNow };
        var flour = new Ingredient { Name = "Flour3", DefaultUnit = "g", Category = "other" };
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
