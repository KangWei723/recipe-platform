using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PantryService.Exceptions;

namespace PantryService.Client;

public class RecipeServiceClient(HttpClient httpClient) : IRecipeServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IngredientDto?> GetIngredientAsync(long ingredientId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync($"/api/ingredients/{ingredientId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<IngredientDto>(response, cancellationToken);
    }

    public async Task<RecipeDetailDto?> GetRecipeAsync(long recipeId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync($"/api/recipes/{recipeId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

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
