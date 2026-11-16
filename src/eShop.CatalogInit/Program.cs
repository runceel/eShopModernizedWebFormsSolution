using eShop.CatalogData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration["ConnectionStrings:catalog"]
    ?? throw new InvalidOperationException("Aspire connection string 'catalog' was not provided.");
builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogInit");
await CatalogSeeder.InitializeAsync(host.Services.GetRequiredService<CatalogDbContext>());
logger.LogInformation("Catalog database migrations and initialization completed.");
