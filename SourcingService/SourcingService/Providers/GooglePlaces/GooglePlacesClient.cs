using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SourcingService.Exceptions;

namespace SourcingService.Providers.GooglePlaces;

public class GooglePlacesClient(HttpClient httpClient, IOptions<GooglePlacesOptions> options) : IGooglePlacesClient
{
    public async Task<IReadOnlyList<GooglePlaceResult>> FindNearbyGroceryStoresAsync(
        double lat, double lng, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var uri = "/maps/api/place/nearbysearch/json" +
                  $"?location={lat},{lng}&radius={config.SearchRadiusMeters}" +
                  $"&type=grocery_or_supermarket&key={Uri.EscapeDataString(config.ApiKey)}";

        var response = await SendAsync(uri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException($"Google Places API returned {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GooglePlacesNearbySearchResponse>(
            cancellationToken: cancellationToken)
            ?? throw new UpstreamServiceException("Google Places API returned an empty body");

        if (payload.Status is not ("OK" or "ZERO_RESULTS"))
        {
            throw new UpstreamServiceException($"Google Places API returned status {payload.Status}");
        }

        return payload.Results ?? [];
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

            throw new UpstreamServiceException("Call to Google Places API failed", ex);
        }
    }
}
