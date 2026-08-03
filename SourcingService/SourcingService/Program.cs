using Messaging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Observability;
using SourcingService.Exceptions;
using SourcingService.Providers;
using SourcingService.Providers.GooglePlaces;
using SourcingService.Providers.Kroger;
using SourcingService.Providers.Mock;
using SourcingService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("sourcing-service", tracing => tracing
    .AddSource(QStashInstrumentation.ActivitySourceName));

builder.Services.Configure<QStashOptions>(builder.Configuration.GetSection("QStash"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<KrogerOptions>(builder.Configuration.GetSection("Kroger"));
builder.Services.Configure<GooglePlacesOptions>(builder.Configuration.GetSection("GooglePlaces"));

// Kroger OAuth2 client-credentials flow: a bare named client used only for
// the token endpoint (Basic auth, handled by hand in KrogerTokenService),
// plus a typed client for Locations/Products that attaches the cached
// bearer token via KrogerAuthHandler. KrogerTokenService is a singleton so
// its in-memory token cache survives across requests.
builder.Services.AddHttpClient("Kroger", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IOptions<KrogerOptions>>().Value.BaseUrl;
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddSingleton<IKrogerTokenService, KrogerTokenService>();
builder.Services.AddTransient<KrogerAuthHandler>();

builder.Services
    .AddHttpClient<IKrogerClient, KrogerClient>((sp, client) =>
    {
        var baseUrl = sp.GetRequiredService<IOptions<KrogerOptions>>().Value.BaseUrl;
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<KrogerAuthHandler>()
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<IGooglePlacesClient, GooglePlacesClient>((sp, client) =>
    {
        var baseUrl = sp.GetRequiredService<IOptions<GooglePlacesOptions>>().Value.BaseUrl;
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services.AddScoped<IStoreProvider, KrogerStoreProvider>();
builder.Services.AddScoped<IStoreProvider, GooglePlacesStoreProvider>();
builder.Services.AddSingleton<MockStoreProvider>();

builder.Services.AddScoped<ISourcingAggregatorService, SourcingAggregatorService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UpstreamServiceException => (StatusCodes.Status502BadGateway, "Upstream service call failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
            detail = exception?.Message
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/actuator/health");
app.MapControllers();

app.Run();

public partial class Program;
