using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Exceptions;

namespace Gateway.Client;

public class RecipeServiceClient(HttpClient httpClient) : IRecipeServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/recipes/{recipeId}";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<RecipeDetailDto>(response, requestUri, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/recipes";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<List<RecipeSummaryDto>>(response, requestUri, cancellationToken);
    }

    public async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/ingredients";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<List<IngredientDto>>(response, requestUri, cancellationToken);
    }

    public async Task<RecipeDetailDto> CreateAsync(
        CreateRecipeDto request, CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/recipes";
        var response = await SendAsync(HttpMethod.Post, requestUri, request, cancellationToken);
        return await ReadOrThrowAsync<RecipeDetailDto>(response, requestUri, cancellationToken);
    }

    public async Task<RecipeDetailDto> UpdateAsync(
        long recipeId, UpdateRecipeDto request, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/recipes/{recipeId}";
        var response = await SendAsync(HttpMethod.Put, requestUri, request, cancellationToken);
        return await ReadOrThrowAsync<RecipeDetailDto>(response, requestUri, cancellationToken);
    }

    public async Task DeleteAsync(long recipeId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/recipes/{recipeId}";
        var response = await SendAsync(HttpMethod.Delete, requestUri, cancellationToken: cancellationToken);
        await ThrowIfErrorAsync(response, requestUri, cancellationToken);
    }

    public async Task<IReadOnlyList<MeasurementUnitDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/ingredients/units";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<List<MeasurementUnitDto>>(response, requestUri, cancellationToken);
    }

    public async Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/ingredients/categories";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<List<IngredientCategoryDto>>(response, requestUri, cancellationToken);
    }

    public async Task<IngredientDto> CreateIngredientAsync(
        CreateIngredientDto request, CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/ingredients";
        var response = await SendAsync(HttpMethod.Post, requestUri, request, cancellationToken);
        return await ReadOrThrowAsync<IngredientDto>(response, requestUri, cancellationToken);
    }

    public async Task<IngredientDto> UpdateIngredientAsync(
        long ingredientId, UpdateIngredientDto request, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/ingredients/{ingredientId}";
        var response = await SendAsync(HttpMethod.Put, requestUri, request, cancellationToken);
        return await ReadOrThrowAsync<IngredientDto>(response, requestUri, cancellationToken);
    }

    public async Task DeleteIngredientAsync(long ingredientId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/ingredients/{ingredientId}";
        var response = await SendAsync(HttpMethod.Delete, requestUri, cancellationToken: cancellationToken);
        await ThrowIfErrorAsync(response, requestUri, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string requestUri, object? body = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(method, requestUri);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamServiceException($"Call to recipe-service failed: {requestUri}", ex);
        }
    }

    private static async Task<T> ReadOrThrowAsync<T>(
        HttpResponseMessage response, string requestUri, CancellationToken cancellationToken)
    {
        await ThrowIfErrorAsync(response, requestUri, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"recipe-service returned an empty body for {requestUri}");
    }

    // recipe-service's global exception handler returns a ProblemDetails-shaped body
    // ({title, status, detail}) for 4xx responses -- surface its "detail" (e.g. "Cannot delete
    // ingredient: used in 3 recipes") as the exception message instead of a generic "returned
    // 409" so it reaches the end user readably through the GraphQL error filter.
    private static async Task ThrowIfErrorAsync(
        HttpResponseMessage response, string requestUri, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await TryReadProblemDetailAsync(response, cancellationToken);
        throw new UpstreamServiceException(
            detail ?? $"recipe-service returned {(int)response.StatusCode} for {requestUri}");
    }

    private static async Task<string?> TryReadProblemDetailAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailBody>(JsonOptions, cancellationToken);
            return problem?.Detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record ProblemDetailBody(string? Title, int? Status, string? Detail);
}
