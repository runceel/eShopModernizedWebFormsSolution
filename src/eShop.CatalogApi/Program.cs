using System.Data.Common;
using eShop.CatalogApi.Endpoints;
using eShop.CatalogApi.Middleware;
using eShop.CatalogApi.Services;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException("Aspire connection string 'catalog' was not provided.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddDbContext<CatalogDbContext>((serviceProvider, options) =>
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/catalog")
            && context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
        {
            context.ProblemDetails.Extensions.TryAdd("code", "validation_error");
        }
    };
});
builder.Services.AddExceptionHandler<CatalogApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<CatalogDatabaseHealthCheck>("catalog-database");

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCatalogCsrfProtection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapCatalogEndpoints();

app.Run();

sealed class CatalogDatabaseHealthCheck(CatalogDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingMigrations = await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                return HealthCheckResult.Unhealthy("Catalog database migrations are pending.");
            }

            var initialized = await dbContext.CatalogInitializations
                .AsNoTracking()
                .AnyAsync(marker => marker.Id == 1, cancellationToken);
            return initialized
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Catalog initialization has not completed.");
        }
        catch (DbException exception)
        {
            return HealthCheckResult.Unhealthy("Catalog database is not ready.", exception);
        }
    }
}

/// <summary>The Catalog API application entry point.</summary>
public partial class Program { }
