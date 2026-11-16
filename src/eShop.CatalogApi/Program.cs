using System.Data.Common;
using eShop.CatalogData;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException("Aspire connection string 'catalog' was not provided.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<CatalogDatabase>();
builder.Services.AddHealthChecks()
    .AddCheck<CatalogDatabaseHealthCheck>("catalog-database");

var app = builder.Build();
app.MapDefaultEndpoints();

app.Run();

sealed class CatalogDatabaseHealthCheck(CatalogDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (DbException exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.", exception);
        }
    }
}
