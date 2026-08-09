using System.Net;
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
        var response = await SendAsync($"/api/recipes/{recipeId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<RecipeDetailDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("/api/recipes", cancellationToken);
        return await ReadOrThrowAsync<List<RecipeSummaryDto>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("/api/ingredients", cancellationToken);
        return await ReadOrThrowAsync<List<IngredientDto>>(response, cancellationToken);
    }

    public async Task<RecipeDetailDto> CreateAsync(
        CreateRecipeDto request, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync("/api/recipes", request, cancellationToken);
        return await ReadOrThrowAsync<RecipeDetailDto>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(requestUri, cancellationToken);
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

    private async Task<HttpResponseMessage> PostAsync<TBody>(
        string requestUri, TBody body, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
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

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"recipe-service returned {(int)response.StatusCode} for {response.RequestMessage?.RequestUri}");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"recipe-service returned an empty body for {response.RequestMessage?.RequestUri}");
    }
}
