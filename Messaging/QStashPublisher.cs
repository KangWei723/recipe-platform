using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

public class QStashPublisher(
    HttpClient httpClient,
    IOptions<QStashOptions> options,
    ILogger<QStashPublisher> logger) : IQStashPublisher
{
    public async Task PublishAsync<T>(string urlGroup, T payload, CancellationToken ct = default)
    {
        using var activity = QStashInstrumentation.ActivitySource.StartActivity(
            $"{urlGroup} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "qstash");
        activity?.SetTag("messaging.destination.name", urlGroup);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}/v2/publish/{urlGroup}")
        {
            Content = JsonContent.Create(payload, options: QStashJson.Options)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Token);

        // QStash strips the "Upstash-Forward-" prefix and delivers the header as-is to each
        // destination, so the receiving webhook sees a normal W3C `traceparent` header and
        // ASP.NET Core's own request activity picks it up as its parent automatically.
        if (activity?.Id is { } traceparent)
        {
            request.Headers.Add("Upstash-Forward-traceparent", traceparent);
        }

        var response = await httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"QStash publish failed: {response.StatusCode}");
            logger.LogWarning(
                "QStash publish to url group {UrlGroup} failed with {StatusCode}: {Body}",
                urlGroup, response.StatusCode, responseBody);
            throw new HttpRequestException($"QStash publish to '{urlGroup}' failed with {response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Published to QStash url group {UrlGroup}: {Response}", urlGroup, responseBody);
    }

    public async Task EnsureUrlGroupAsync(
        string urlGroup, IEnumerable<(string Name, string Url)> endpoints, CancellationToken ct = default)
    {
        var endpointList = endpoints.ToList();
        var body = new
        {
            endpoints = endpointList.Select(e => new { name = e.Name, url = e.Url })
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}/v2/topics/{urlGroup}/endpoints")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Token);

        var response = await httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"QStash URL group setup for '{urlGroup}' failed with {response.StatusCode}: {responseBody}");
        }

        logger.LogInformation(
            "Registered QStash url group {UrlGroup} with endpoints: {Endpoints}",
            urlGroup, string.Join(", ", endpointList.Select(e => $"{e.Name}={e.Url}")));
    }
}
