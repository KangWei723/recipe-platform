using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using SourcingService.Exceptions;

namespace SourcingService.Providers.Kroger;

// Kroger's client-credentials tokens are valid ~30 minutes; caching avoids a
// token round trip on every Locations/Products call. This service is
// registered as a singleton (see Program.cs) so the cache actually persists
// across requests. Refresh is guarded by a semaphore so concurrent requests
// near expiry don't all fire refreshes at once.
public class KrogerTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<KrogerOptions> options,
    ILogger<KrogerTokenService> logger) : IKrogerTokenService
{
    private const int ExpiryBufferSeconds = 60;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _cachedToken;
            }

            return await RefreshTokenAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<string> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        var client = httpClientFactory.CreateClient("Kroger");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/connect/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = config.Scope
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamServiceException("Failed to reach Kroger's OAuth2 token endpoint", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException($"Kroger token endpoint returned {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<KrogerTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new UpstreamServiceException("Kroger token endpoint returned an empty body");

        _cachedToken = payload.AccessToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - ExpiryBufferSeconds);

        logger.LogInformation("Refreshed Kroger OAuth2 token; expires in {ExpiresIn}s", payload.ExpiresIn);

        return _cachedToken;
    }
}
