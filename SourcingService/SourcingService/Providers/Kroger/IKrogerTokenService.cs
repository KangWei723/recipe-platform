namespace SourcingService.Providers.Kroger;

public interface IKrogerTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
