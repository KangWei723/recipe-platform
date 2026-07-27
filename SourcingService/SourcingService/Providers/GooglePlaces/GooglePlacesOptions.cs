namespace SourcingService.Providers.GooglePlaces;

public class GooglePlacesOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int SearchRadiusMeters { get; set; } = 5000;
}
