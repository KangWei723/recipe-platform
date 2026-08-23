using FluentAssertions;
using Moq;
using RecipeService.Domain;
using RecipeService.Repositories;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Services;

public class RecipesServiceTests
{
    private readonly Mock<IRecipeRepository> _recipeRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IIngredientRepository> _ingredientRepository = new();
    private readonly RecipesService _service;

    public RecipesServiceTests()
    {
        _service = new RecipesService(_recipeRepository.Object, _userRepository.Object, _ingredientRepository.Object);
    }

    private static Recipe MakeRecipe(long id, string title, params (long ingredientId, bool optional)[] ingredients) =>
        new()
        {
            Id = id,
            AuthorId = 1,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            Ingredients = ingredients
                .Select(i => new RecipeIngredient
                {
                    IngredientId = i.ingredientId,
                    Optional = i.optional,
                    Unit = "g",
                    Quantity = 1,
                    Ingredient = new Ingredient { Id = i.ingredientId, Name = $"Ingredient {i.ingredientId}" }
                })
                .ToList()
        };

    [Fact]
    public async Task GetMatchesAsync_RanksFullMatchesBeforePartialMatches()
    {
        var fullMatch = MakeRecipe(1, "Full Match", (10, false), (11, false));
        var partialMatch = MakeRecipe(2, "Partial Match", (10, false), (12, false));
        _recipeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([partialMatch, fullMatch]);

        var result = await _service.GetMatchesAsync([10, 11]);

        result.Select(m => m.Id).Should().ContainInOrder(1, 2);
        result[0].MissingIngredients.Should().BeEmpty();
        result[1].MissingIngredients.Should().ContainSingle(m => m.IngredientId == 12);
    }

    [Fact]
    public async Task GetMatchesAsync_WhenMissingCountsTie_RanksMoreMatchedIngredientsFirst()
    {
        var smaller = MakeRecipe(1, "Smaller", (10, false), (99, false));
        var bigger = MakeRecipe(2, "Bigger", (10, false), (11, false), (12, false), (99, false));
        _recipeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([smaller, bigger]);

        var result = await _service.GetMatchesAsync([10, 11, 12]);

        result.Select(m => m.Id).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public async Task GetMatchesAsync_OptionalIngredientsAreExcludedFromCountsAndNeverMissing()
    {
        var recipe = MakeRecipe(1, "Has Optional", (10, false), (20, true));
        _recipeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([recipe]);

        var result = await _service.GetMatchesAsync([10]);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            RequiredIngredientCount = 1,
            MatchedIngredientCount = 1,
            MissingIngredients = Array.Empty<object>()
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task GetMatchesAsync_WithNoIngredientsSelected_EveryRequiredIngredientIsMissing()
    {
        var recipe = MakeRecipe(1, "Needs Everything", (10, false), (11, false));
        _recipeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([recipe]);

        var result = await _service.GetMatchesAsync([]);

        var match = result.Should().ContainSingle().Subject;
        match.MatchedIngredientCount.Should().Be(0);
        match.MissingIngredients.Should().HaveCount(2);
    }
}
