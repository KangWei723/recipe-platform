namespace Messaging;

public class QStashOptions
{
    public string BaseUrl { get; set; } = "https://qstash.upstash.io";
    public string Token { get; set; } = "";
    public string CurrentSigningKey { get; set; } = "";
    public string NextSigningKey { get; set; } = "";

    // Only set by services that receive webhooks (must exactly match the URL
    // registered as this service's endpoint in the publisher's URL group,
    // since QStash puts that same URL in the JWT's `sub` claim).
    public string WebhookUrl { get; set; } = "";
}
