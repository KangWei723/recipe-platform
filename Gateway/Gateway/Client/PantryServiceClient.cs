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

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamServiceException($"Call to pantry-service failed: {requestUri}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"pantry-service returned {(int)response.StatusCode} for {requestUri}");
        }

        var result = await response.Content.ReadFromJsonAsync<MissingIngredientsDto>(JsonOptions, cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"pantry-service returned an empty body for {requestUri}");
    }
}
