using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Exceptions;

namespace Gateway.Client;

public class SubstitutionServiceClient(HttpClient httpClient) : ISubstitutionServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<RankedSubstituteDto>> GetRankedSubstitutesAsync(
        string ingredientName, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/substitutions/{Uri.EscapeDataString(ingredientName)}/ranked";

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

            throw new UpstreamServiceException($"Call to substitution-service failed: {requestUri}", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                $"substitution-service returned {(int)response.StatusCode} for {requestUri}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<List<RankedSubstituteDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }
}
