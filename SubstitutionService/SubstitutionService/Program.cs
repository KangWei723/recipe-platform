using Microsoft.AspNetCore.Diagnostics;
using Neo4j.Driver;
using Observability;
using SubstitutionService.Data;
using SubstitutionService.Exceptions;
using SubstitutionService.Repositories;
using SubstitutionService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("substitution-service");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IDriver>(_ =>
{
    var config = builder.Configuration.GetSection("Neo4j");
    return GraphDatabase.Driver(
        config["Uri"],
        AuthTokens.Basic(config["Username"], config["Password"]));
});

builder.Services.AddHealthChecks()
    .AddCheck<Neo4jHealthCheck>("neo4j");

builder.Services.AddScoped<ISubstitutionRepository, SubstitutionRepository>();
builder.Services.AddScoped<ISubstitutionsService, SubstitutionsService>();

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
