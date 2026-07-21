using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PantryService.Client;
using PantryService.Data;
using PantryService.Exceptions;
using PantryService.Repositories;
using PantryService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PantryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PantryDb")));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PantryDb")!);

builder.Services.AddScoped<IPantryItemRepository, PantryItemRepository>();
builder.Services.AddScoped<IPantryItemsService, PantryItemsService>();

builder.Services
    .AddHttpClient<IRecipeServiceClient, RecipeServiceClient>(client =>
    {
        var baseUrl = builder.Configuration["RecipeService:BaseUrl"]
            ?? throw new InvalidOperationException("RecipeService:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler();

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

app.MapHealthChecks("/actuator/health");
app.MapControllers();

app.Run();

public partial class Program;
