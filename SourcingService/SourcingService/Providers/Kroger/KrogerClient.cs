using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SourcingService.Exceptions;

namespace SourcingService.Providers.Kroger;

public class KrogerClient(HttpClient httpClient, IOptions<KrogerOptions> options) : IKrogerClient
{
    public async Task<KrogerLocation?> FindNearestLocationAsync(
        double lat, double lng, CancellationToken cancellationToken)
    {
        var radius = options.Value.SearchRadiusMiles;
        var uri = $"/v1/locations?filter.lat.near={lat}&filter.lon.near={lng}" +
                  $"&filter.radiusInMiles={radius}&filter.limit=1";

        var response = await SendAsync(uri, cancellationToken);
        var payload = await ReadOrThrowAsync<KrogerLocationsResponse>(response, cancellationToken);
        return payload.Data.FirstOrDefault();
    }

    public async Task<IReadOnlyList<KrogerProduct>> SearchProductsAsync(
        string term, string locationId, CancellationToken cancellationToken)
    {
        var uri = $"/v1/products?filter.term={Uri.EscapeDataString(term)}" +
                  $"&filter.locationId={locationId}&filter.limit=5";

        var response = await SendAsync(uri, cancellationToken);
        var payload = await ReadOrThrowAsync<KrogerProductsResponse>(response, cancellationToken);
        return payload.Data;
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

            throw new UpstreamServiceException($"Call to Kroger API failed: {requestUri}", ex);
        }
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"Kroger API returned {(int)response.StatusCode} for {response.RequestMessage?.RequestUri}");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return result ?? throw new UpstreamServiceException(
            $"Kroger API returned an empty body for {response.RequestMessage?.RequestUri}");
    }
}
