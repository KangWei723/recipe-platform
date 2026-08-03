namespace Messaging;

public interface IQStashPublisher
{
    /// <summary>Publishes payload to every endpoint currently registered in the given URL group.</summary>
    Task PublishAsync<T>(string urlGroup, T payload, CancellationToken ct = default);

    /// <summary>Idempotently registers/updates the named endpoints on a URL group (QStash's fan-out primitive).</summary>
    Task EnsureUrlGroupAsync(string urlGroup, IEnumerable<(string Name, string Url)> endpoints, CancellationToken ct = default);
}
