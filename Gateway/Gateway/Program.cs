using Gateway.Client;
using Gateway.GraphQL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGateway();

builder.Services
    .AddHttpClient<IRecipeServiceClient, RecipeServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["RecipeService:BaseUrl"]
            ?? throw new InvalidOperationException("RecipeService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<IPantryServiceClient, PantryServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["PantryService:BaseUrl"]
            ?? throw new InvalidOperationException("PantryService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<ISubstitutionServiceClient, SubstitutionServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["SubstitutionService:BaseUrl"]
            ?? throw new InvalidOperationException("SubstitutionService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<ISourcingServiceClient, SourcingServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["SourcingService:BaseUrl"]
            ?? throw new InvalidOperationException("SourcingService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services.AddHealthChecks();

// No auth exists anywhere in the platform yet (see docs/design.md Phase 1
// scope), so this is a permissive dev-friendly policy rather than a
// locked-down one. Revisit once the client app's origin is known.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddTypeExtension<RecipeIngredientResolvers>()
    .AddErrorFilter<UpstreamServiceErrorFilter>()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseCors();

app.MapHealthChecks("/actuator/health");
app.MapGraphQL("/graphql");

app.Run();

public partial class Program;
