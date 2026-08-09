using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;

namespace Gateway.Client;

public class SubstitutionServiceClient(HttpClient httpClient, ILogger<SubstitutionServiceClient> logger)
    : ISubstitutionServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Substitutions are a non-critical enhancement to the recipe view (see
    // RecipeIngredientResolvers.GetSubstitutionsAsync) -- unlike the other upstream clients, any
    // failure here (unreachable, slow/timed out via the short timeout configured on this
    // HttpClient in Program.cs, or a non-2xx response) degrades to an empty list instead of
    // throwing, so a struggling substitution-service/Neo4j never blocks the whole recipe query.
    // Mirrors SourcingService's RedisSourcingCache, which treats any Redis exception the same way.
    public async Task<IReadOnlyList<RankedSubstituteDto>> GetRankedSubstitutesAsync(
        string ingredientName, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/substitutions/{Uri.EscapeDataString(ingredientName)}/ranked";

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
                "substitution-service unreachable/timed out for {RequestUri}; returning empty substitutions",
                requestUri);
            return [];
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "substitution-service returned {StatusCode} for {RequestUri}; returning empty substitutions",
                (int)response.StatusCode, requestUri);
            return [];
        }

        var result = await response.Content
            .ReadFromJsonAsync<List<RankedSubstituteDto>>(JsonOptions, cancellationToken);
        return result ?? [];
    }
}
