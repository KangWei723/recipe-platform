using Auth;
using Gateway.Client;
using Gateway.GraphQL;
using HotChocolate.Authorization;
using Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("gateway");
builder.AddAuth0Authentication();

builder.Services.AddGateway();

// Each backend service still validates the forwarded bearer token itself and is the real
// enforcement point -- Gateway authenticating too isn't a substitute for that, it just lets
// Gateway reject with a clean 401/GraphQL auth error (and, via the AdminOnly policy on specific
// mutations below, a clean 403) instead of every request round-tripping to a backend only to
// bounce off its auth check. What Gateway must also keep doing is forward the original
// Authorization header onto every downstream call, or those calls would arrive anonymous.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthHeaderForwardingHandler>();

builder.Services
    .AddHttpClient<IRecipeServiceClient, RecipeServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["RecipeService:BaseUrl"]
            ?? throw new InvalidOperationException("RecipeService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<IPantryServiceClient, PantryServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["PantryService:BaseUrl"]
            ?? throw new InvalidOperationException("PantryService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<ISubstitutionServiceClient, SubstitutionServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["SubstitutionService:BaseUrl"]
            ?? throw new InvalidOperationException("SubstitutionService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
    .AddStandardResilienceHandler(options =>
    {
        // Substitutions are a non-critical enhancement (see SubstitutionServiceClient) -- fail
        // fast instead of the other clients' default 30s total-request budget, so a struggling
        // substitution-service/Neo4j can't stall the whole recipe query.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(3);
    });

builder.Services
    .AddHttpClient<ISourcingServiceClient, SourcingServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["SourcingService:BaseUrl"]
            ?? throw new InvalidOperationException("SourcingService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
    .AddStandardResilienceHandler(options =>
    {
        // Nearby-store lookups are a non-critical enhancement (see SourcingServiceClient) --
        // fail fast instead of the other clients' default 30s total-request budget, same
        // reasoning as SubstitutionService's client above. Budget is looser than
        // SubstitutionService's (2s/3s) because sourcing-service legitimately fans out to two
        // real external providers (Kroger, Google Places) in parallel with its own ~3s
        // per-provider timeout -- this must comfortably exceed that normal-case duration.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);
    });

builder.Services.AddHealthChecks();

// Real auth exists now (see docs/design.md's "Production usage shift" section) -- origins
// are an explicit config-driven allowlist rather than the old AllowAnyOrigin dev placeholder.
// No frontend app exists in this repo yet, so the default only covers common local SPA dev
// ports; add the real deployed origin(s) to Cors:AllowedOrigins once that app exists.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services
    .AddGraphQLServer()
    .AddAuthorizationCore()
    .AddAuthorizationHandler<GraphQLAuthorizationHandler>()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddTypeExtension<RecipeIngredientResolvers>()
    .AddErrorFilter<UpstreamServiceErrorFilter>()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/actuator/health").AllowAnonymous();
app.MapGraphQL("/graphql");

app.Run();

public partial class Program;
