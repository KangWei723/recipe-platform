using Auth;
using Messaging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Observability;
using OpenTelemetry.Trace;
using PantryService.Auth;
using PantryService.Client;
using PantryService.Data;
using PantryService.Exceptions;
using PantryService.Repositories;
using PantryService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("pantry-service", tracing => tracing
    .AddEntityFrameworkCoreInstrumentation()
    .AddSource(QStashInstrumentation.ActivitySourceName));
builder.AddAuth0Authentication();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PantryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PantryDb")));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PantryDb")!);

builder.Services.AddScoped<IPantryItemRepository, PantryItemRepository>();
builder.Services.AddScoped<IPantryItemsService, PantryItemsService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthHeaderForwardingHandler>();
builder.Services.AddSingleton<ICurrentUserCache, CurrentUserCache>();
builder.Services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();

builder.Services
    .AddHttpClient<IRecipeServiceClient, RecipeServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["RecipeService:BaseUrl"]
            ?? throw new InvalidOperationException("RecipeService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    // Forwards this request's own bearer token to recipe-service so /api/users/me resolves
    // the same caller, rather than calling as an anonymous/unrelated identity.
    .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
    .AddStandardResilienceHandler();

builder.Services.Configure<QStashOptions>(builder.Configuration.GetSection("QStash"));
builder.Services.AddHttpClient<IQStashPublisher, QStashPublisher>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/actuator/health").AllowAnonymous();
app.MapControllers();

var qstashConsumers = app.Configuration.GetSection("QStash:Consumers").GetChildren()
    .Where(c => !string.IsNullOrEmpty(c.Value))
    .Select(c => (Name: c.Key, Url: c.Value!))
    .ToList();

if (qstashConsumers.Count > 0)
{
    try
    {
        var publisher = app.Services.GetRequiredService<IQStashPublisher>();
        await publisher.EnsureUrlGroupAsync(QStashTopics.IngredientMissing, qstashConsumers);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "Failed to register QStash url group '{UrlGroup}' at startup — is the QStash server at {BaseUrl} reachable?",
            QStashTopics.IngredientMissing, app.Configuration["QStash:BaseUrl"]);
    }
}

app.Run();

public partial class Program;
