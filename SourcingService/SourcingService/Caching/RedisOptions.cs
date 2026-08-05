namespace SourcingService.Caching;

public class RedisOptions
{
    public string ConnectionString { get; set; } = "";
    public double CacheTtlMinutes { get; set; } = 7;
}
