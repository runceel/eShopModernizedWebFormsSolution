using eShop.CatalogData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration["ConnectionStrings:catalog"]
    ?? throw new InvalidOperationException("Aspire connection string 'catalog' was not provided.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<CatalogDatabase>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogInit");
await host.Services.GetRequiredService<CatalogDatabase>().CheckConnectionAsync();
logger.LogInformation(
    "PostgreSQL connectivity verified. Schema migrations and catalog seed data are not yet implemented.");
