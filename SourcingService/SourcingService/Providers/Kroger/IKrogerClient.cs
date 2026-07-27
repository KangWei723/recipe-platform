namespace SourcingService.Providers.Kroger;

public interface IKrogerClient
{
    Task<KrogerLocation?> FindNearestLocationAsync(double lat, double lng, CancellationToken cancellationToken);

    Task<IReadOnlyList<KrogerProduct>> SearchProductsAsync(
        string term, string locationId, CancellationToken cancellationToken);
}
