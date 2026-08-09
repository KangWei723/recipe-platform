using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Exceptions;

namespace Gateway.Client;

public class PantryServiceClient(HttpClient httpClient) : IPantryServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MissingIngredientsDto> GetMissingIngredientsAsync(
        long recipeId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/pantry/recipes/{recipeId}/missing-ingredients";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<MissingIngredientsDto>(response, requestUri, cancellationToken);
    }

    public async Task<IReadOnlyList<PantryItemDto>> GetForUserAsync(CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/pantry";
        var response = await SendAsync(HttpMethod.Get, requestUri, cancellationToken: cancellationToken);
        return await ReadOrThrowAsync<List<PantryItemDto>>(response, requestUri, cancellationToken);
    }

    public async Task<PantryItemDto> UpsertAsync(
        UpsertPantryItemDto request, CancellationToken cancellationToken = default)
    {
        const string requestUri = "/api/pantry/items";
        var response = await SendAsync(HttpMethod.Put, requestUri, request, cancellationToken);
        return await ReadOrThrowAsync<PantryItemDto>(response, requestUri, cancellationToken);
    }

    public async Task DeleteAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/pantry/items/{itemId}";
        var response = await SendAsync(HttpMethod.Delete, requestUri, cancellationToken: cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"pantry-service returned {(int)response.StatusCode} for {requestUri}");
        }
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

            throw new UpstreamServiceException($"Call to pantry-service failed: {requestUri}", ex);
        }
    }

    private static async Task<T> ReadOrThrowAsync<T>(
        HttpResponseMessage response, string requestUri, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"pantry-service returned {(int)response.StatusCode} for {requestUri}");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"pantry-service returned an empty body for {requestUri}");
    }
}
