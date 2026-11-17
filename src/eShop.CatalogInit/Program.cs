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

var useCustomizationData = ReadUseCustomizationData(builder.Configuration["CATALOG_USE_CUSTOMIZATION_DATA"]);
var customizationDirectory = builder.Configuration["CATALOG_CUSTOMIZATION_DIRECTORY"];
var imageDirectory = builder.Configuration["CATALOG_IMAGES_PATH"];

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogInit");
await CatalogSeeder.InitializeConfiguredAsync(
    host.Services.GetRequiredService<CatalogDbContext>(),
    useCustomizationData,
    customizationDirectory,
    imageDirectory);
logger.LogInformation(
    "Catalog database migrations and initialization completed using {DataSource} data.",
    useCustomizationData ? "customization" : "built-in");

static bool ReadUseCustomizationData(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    if (bool.TryParse(value, out var enabled))
    {
        return enabled;
    }

    throw new InvalidOperationException("CATALOG_USE_CUSTOMIZATION_DATA must be true or false.");
}
