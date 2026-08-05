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

    public async Task<UserDto> ResolveCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/users/me", cancellationToken);
        return await ReadOrThrowAsync<UserDto>(response, cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(string requestUri, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, requestUri, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, requestUri);
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
