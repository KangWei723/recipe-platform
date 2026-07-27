namespace SourcingService.Providers.Kroger;

public class KrogerOptions
{
    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "product.compact";
    public double SearchRadiusMiles { get; set; } = 10;
}
