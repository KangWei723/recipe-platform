using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Exceptions;
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

    // A second instance with the Cloudinary-host restriction turned on (off by default -- see
    // CloudinaryOptions.RestrictImageUrlsToOwnCloud), used only by the tests that specifically
    // exercise that restriction. Every other test uses _service, matching today's default.
    private readonly RecipesService _serviceWithCloudinaryRestriction;
    private const string ConfiguredCloudName = "demo-cloud";

    public RecipesServiceTests()
    {
        _service = new RecipesService(
            _recipeRepository.Object, _userRepository.Object, _ingredientRepository.Object,
            Options.Create(new CloudinaryOptions()));

        _serviceWithCloudinaryRestriction = new RecipesService(
            _recipeRepository.Object, _userRepository.Object, _ingredientRepository.Object,
            Options.Create(new CloudinaryOptions { CloudName = ConfiguredCloudName, RestrictImageUrlsToOwnCloud = true }));

        // Default passthrough setups used by the CreateAsync/UpdateAsync validation tests below --
        // every one of them uses an empty Ingredients list and a valid author, so these cover that
        // without each test repeating it.
        _ingredientRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<long>>())).ReturnsAsync([]);
        _userRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync(new User { Id = 1, Email = "author@example.com", Name = "Author", CreatedAt = DateTimeOffset.UtcNow });
    }

    private static CreateRecipeRequest MakeCreateRequest(
        string? imageUrl = null, string? stepImageUrl = null, List<string>? tips = null, string? pairing = null) =>
        new(
            Title: "Test Recipe",
            Description: null,
            Servings: null,
            PrepTimeMin: null,
            CookTimeMin: null,
            ImageUrl: imageUrl,
            Steps: [new CreateRecipeStepRequest(1, "Step one", null, stepImageUrl)],
            Ingredients: [],
            Tips: tips,
            Pairing: pairing
        );

    private static UpdateRecipeRequest MakeUpdateRequest(
        string? imageUrl = null, string? stepImageUrl = null, List<string>? tips = null, string? pairing = null) =>
        new(
            Title: "Test Recipe",
            Description: null,
            Servings: null,
            PrepTimeMin: null,
            CookTimeMin: null,
            ImageUrl: imageUrl,
            Steps: [new CreateRecipeStepRequest(1, "Step one", null, stepImageUrl)],
            Ingredients: [],
            Tips: tips,
            Pairing: pairing
        );

    // Deliberately well over ImageUrlValidator.MaxLength (2048) regardless of the scheme/host
    // prefix length.
    private static readonly string TooLongImageUrl = "https://example.com/" + new string('a', 2048);

    [Fact]
    public async Task CreateAsync_WithValidHttpsImageUrls_Succeeds()
    {
        var request = MakeCreateRequest(imageUrl: "https://cdn.example.com/hero.jpg", stepImageUrl: "https://cdn.example.com/step.jpg");
        _recipeRepository.Setup(r => r.AddAsync(It.IsAny<Recipe>())).ReturnsAsync((Recipe r) => r);
        _recipeRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new Recipe { Id = 1, AuthorId = 1, Title = "Test Recipe" });

        await _service.CreateAsync(request, authorId: 1);

        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenCloudinaryRestrictionEnabled_AndImageUrlFromDifferentHost_ThrowsValidation()
    {
        var request = MakeCreateRequest(imageUrl: "https://cdn.example.com/hero.jpg");

        var act = () => _serviceWithCloudinaryRestriction.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenCloudinaryRestrictionEnabled_AndImageUrlFromConfiguredCloud_Succeeds()
    {
        var request = MakeCreateRequest(
            imageUrl: $"https://res.cloudinary.com/{ConfiguredCloudName}/image/upload/v1/hero.jpg");
        _recipeRepository.Setup(r => r.AddAsync(It.IsAny<Recipe>())).ReturnsAsync((Recipe r) => r);
        _recipeRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new Recipe { Id = 1, AuthorId = 1, Title = "Test Recipe" });

        await _serviceWithCloudinaryRestriction.CreateAsync(request, authorId: 1);

        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Once);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://insecure.example/x.png")]
    public async Task CreateAsync_WhenHeroImageUrlInvalid_ThrowsValidation(string badUrl)
    {
        var request = MakeCreateRequest(imageUrl: badUrl);

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenHeroImageUrlTooLong_ThrowsValidation()
    {
        var request = MakeCreateRequest(imageUrl: TooLongImageUrl);

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://insecure.example/x.png")]
    public async Task CreateAsync_WhenStepImageUrlInvalid_ThrowsValidation(string badUrl)
    {
        var request = MakeCreateRequest(stepImageUrl: badUrl);

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenStepImageUrlTooLong_ThrowsValidation()
    {
        var request = MakeCreateRequest(stepImageUrl: TooLongImageUrl);

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://insecure.example/x.png")]
    public async Task UpdateAsync_WhenHeroImageUrlInvalid_ThrowsValidation(string badUrl)
    {
        var request = MakeUpdateRequest(imageUrl: badUrl);

        var act = () => _service.UpdateAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();
        _recipeRepository.Verify(
            r => r.UpdateAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<List<RecipeStep>>(), It.IsAny<List<RecipeIngredient>>(),
                It.IsAny<List<string>>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://insecure.example/x.png")]
    public async Task UpdateAsync_WhenStepImageUrlInvalid_ThrowsValidation(string badUrl)
    {
        var request = MakeUpdateRequest(stepImageUrl: badUrl);

        var act = () => _service.UpdateAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenHeroImageUrlTooLong_ThrowsValidation()
    {
        var request = MakeUpdateRequest(imageUrl: TooLongImageUrl);

        var act = () => _service.UpdateAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_WhenMoreThanTenTips_ThrowsValidation()
    {
        var request = MakeCreateRequest(tips: Enumerable.Range(1, 11).Select(i => $"Tip {i}").ToList());

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_WithExactlyTenTips_Succeeds()
    {
        var request = MakeCreateRequest(tips: Enumerable.Range(1, 10).Select(i => $"Tip {i}").ToList());
        _recipeRepository.Setup(r => r.AddAsync(It.IsAny<Recipe>())).ReturnsAsync((Recipe r) => r);
        _recipeRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new Recipe { Id = 1, AuthorId = 1, Title = "Test Recipe" });

        await _service.CreateAsync(request, authorId: 1);

        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenTipExceedsMaxLength_ThrowsValidation()
    {
        var request = MakeCreateRequest(tips: [new string('a', 301)]);

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_WithTipAtExactlyMaxLength_Succeeds()
    {
        var request = MakeCreateRequest(tips: [new string('a', 300)]);
        _recipeRepository.Setup(r => r.AddAsync(It.IsAny<Recipe>())).ReturnsAsync((Recipe r) => r);
        _recipeRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new Recipe { Id = 1, AuthorId = 1, Title = "Test Recipe" });

        await _service.CreateAsync(request, authorId: 1);

        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPairingExceedsMaxLength_ThrowsValidation()
    {
        var request = MakeCreateRequest(pairing: new string('a', 501));

        var act = () => _service.CreateAsync(request, authorId: 1);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_WithPairingAtExactlyMaxLength_Succeeds()
    {
        var request = MakeCreateRequest(pairing: new string('a', 500));
        _recipeRepository.Setup(r => r.AddAsync(It.IsAny<Recipe>())).ReturnsAsync((Recipe r) => r);
        _recipeRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new Recipe { Id = 1, AuthorId = 1, Title = "Test Recipe" });

        await _service.CreateAsync(request, authorId: 1);

        _recipeRepository.Verify(r => r.AddAsync(It.IsAny<Recipe>()), Times.Once);
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
                    Ingredient = new Ingredient { Id = i.ingredientId, Name = $"Ingredient {i.ingredientId}", Category = "other" }
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
