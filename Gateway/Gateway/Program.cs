using Auth;
using Gateway.Client;
using Gateway.Exceptions;
using Gateway.GraphQL;
using HotChocolate.Authorization;
using Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("gateway");
builder.AddAuth0Authentication();

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
        // fail fast instead of the other clients' default 30s total-request budget, so a
        // struggling sourcing-service can't stall the whole recipe query. Budget is looser
        // (4s/6s) than a tight single-hop timeout because sourcing-service legitimately fans out
        // to two real external providers (Kroger, Google Places) in parallel with its own ~3s
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
    .AddErrorFilter<UpstreamServiceErrorFilter>()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/actuator/health").AllowAnonymous();
app.MapGraphQL("/graphql");

// Plain REST, not a GraphQL mutation -- GraphQL's multipart-upload story (a new Upload scalar,
// a client-side exchange urql doesn't ship by default) would still end up forwarding to
// recipe-service's own REST endpoint underneath, for no benefit over just exposing that forward
// directly here. recipe-service owns the real validation and the Cloudinary call; this is a pure
// passthrough. RequireAuthorization here is the same UX-shortcut-not-the-real-boundary pattern as
// every admin-only GraphQL mutation above -- recipe-service enforces AdminOnly again regardless.
app.MapPost("/api/images/upload", async (IFormFile file, IRecipeServiceClient recipeClient, CancellationToken ct) =>
{
    try
    {
        await using var stream = file.OpenReadStream();
        var url = await recipeClient.UploadImageAsync(stream, file.FileName, file.ContentType, ct);
        return Results.Ok(new { url });
    }
    catch (UpstreamServiceException ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
})
.RequireAuthorization(AuthorizationPolicies.AdminOnly)
// .NET 8 attaches antiforgery metadata to any endpoint that binds IFormFile/form data,
// regardless of whether AddAntiforgery() is registered -- confirmed empirically: without this,
// the endpoint 500s with "contains anti-forgery metadata, but a middleware was not found",
// since Gateway has no app.UseAntiforgery() in its pipeline at all. That protection guards
// against cookie-based session riding; this endpoint is bearer-token-authenticated (the same
// forwarded Authorization header every other Gateway call uses), so there's no cookie session
// for a forged request to ride, and disabling it here doesn't remove any real protection.
.DisableAntiforgery();

app.Run();

public partial class Program;
