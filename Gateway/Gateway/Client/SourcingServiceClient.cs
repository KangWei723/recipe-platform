using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Exceptions;

namespace Gateway.Client;

public class SourcingServiceClient(HttpClient httpClient) : ISourcingServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<NearbySourcingDto> GetNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"/api/sourcing/{Uri.EscapeDataString(ingredientName)}/nearby" +
            $"?lat={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&lng={lng.ToString(CultureInfo.InvariantCulture)}";

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

            throw new UpstreamServiceException($"Call to sourcing-service failed: {requestUri}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"sourcing-service returned {(int)response.StatusCode} for {requestUri}");
        }

        var result = await response.Content.ReadFromJsonAsync<NearbySourcingDto>(JsonOptions, cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"sourcing-service returned an empty body for {requestUri}");
    }
}
