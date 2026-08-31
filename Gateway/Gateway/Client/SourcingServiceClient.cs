using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;

namespace Gateway.Client;

public class SourcingServiceClient(HttpClient httpClient, ILogger<SourcingServiceClient> logger)
    : ISourcingServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Nearby-store lookups are a non-critical enhancement (see Query.ConfirmedStoreOfferAsync /
    // Query.NearbyStoresGeneralAsync) -- any failure here (unreachable, timed out via the scoped
    // timeout configured on this HttpClient in Program.cs, or a non-2xx response) degrades to an
    // empty result instead of throwing. Mirrors SubstitutionServiceClient's fallback.
    public async Task<NearbySourcingDto> GetConfirmedNearbyAsync(
        string ingredientName, double lat, double lng, CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"/api/sourcing/{Uri.EscapeDataString(ingredientName)}/nearby/confirmed" +
            $"?lat={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&lng={lng.ToString(CultureInfo.InvariantCulture)}";

        var result = await GetAsync<NearbySourcingDto>(requestUri, cancellationToken);
        return result ?? new NearbySourcingDto(ingredientName, lat, lng, []);
    }

    public async Task<NearbyGeneralSourcingDto> GetGeneralNearbyAsync(
        double lat, double lng, CancellationToken cancellationToken = default)
    {
        var requestUri =
            "/api/sourcing/nearby/general" +
            $"?lat={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&lng={lng.ToString(CultureInfo.InvariantCulture)}";

        var result = await GetAsync<NearbyGeneralSourcingDto>(requestUri, cancellationToken);
        return result ?? new NearbyGeneralSourcingDto(lat, lng, []);
    }

    private async Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(ex,
                "sourcing-service unreachable/timed out for {RequestUri}; returning empty nearby stores",
                requestUri);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "sourcing-service returned {StatusCode} for {RequestUri}; returning empty nearby stores",
                (int)response.StatusCode, requestUri);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }
}
