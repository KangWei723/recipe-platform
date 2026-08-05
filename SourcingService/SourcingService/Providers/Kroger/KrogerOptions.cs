namespace SourcingService.Providers.Kroger;

// Environment picks which of the two credential/base-URL sets below is active.
// Local dev (start-all.ps1, appsettings.json default) targets Certification;
// Render's production deployment overrides Kroger:Environment=Production and
// supplies Kroger:Production:* via env vars, never checked into the repo.
public class KrogerOptions
{
    public string Environment { get; set; } = "Certification";
    public KrogerEnvironmentOptions Certification { get; set; } = new();
    public KrogerEnvironmentOptions Production { get; set; } = new();
    public string Scope { get; set; } = "product.compact";
    public double SearchRadiusMiles { get; set; } = 10;

    private KrogerEnvironmentOptions Active =>
        Environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ? Production : Certification;

    public string BaseUrl => Active.BaseUrl;
    public string ClientId => Active.ClientId;
    public string ClientSecret => Active.ClientSecret;
}

public class KrogerEnvironmentOptions
{
    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
