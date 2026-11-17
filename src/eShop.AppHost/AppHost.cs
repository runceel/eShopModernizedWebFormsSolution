using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var useCustomizationData = ReadBooleanSetting(
    builder.Configuration["CATALOG_USE_CUSTOMIZATION_DATA"],
    defaultValue: false);
var customizationDirectory = ResolveAppHostPath(
    builder.Configuration["CATALOG_CUSTOMIZATION_DIRECTORY"] ?? "Setup",
    builder.AppHostDirectory);
var localDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
if (string.IsNullOrWhiteSpace(localDataDirectory))
{
    throw new InvalidOperationException("The local application data directory is not available.");
}

var imageDirectory = ResolveAppHostPath(
    builder.Configuration["CATALOG_IMAGES_PATH"]
        ?? Path.Combine(localDataDirectory, "eShopModernizedWebFormsSolution", "catalog-images"),
    builder.AppHostDirectory);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var catalogDb = postgres.AddDatabase("catalog");

var catalogInit = builder.AddProject("catalog-init", "../eShop.CatalogInit/eShop.CatalogInit.csproj")
    .WithReference(catalogDb)
    .WithEnvironment("CATALOG_USE_CUSTOMIZATION_DATA", useCustomizationData.ToString())
    .WithEnvironment("CATALOG_CUSTOMIZATION_DIRECTORY", customizationDirectory)
    .WithEnvironment("CATALOG_IMAGES_PATH", imageDirectory)
    .WaitFor(catalogDb);

var catalogApi = builder.AddProject("catalog-api", "../eShop.CatalogApi/eShop.CatalogApi.csproj")
    .WithReference(catalogDb)
    .WithHttpHealthCheck("/health")
    .WaitForCompletion(catalogInit);

catalogDb.WithCommand(
    "reset-catalog",
    "Reset catalog data",
    ResetCatalogAsync,
    new CommandOptions
    {
        Description = "Replace catalog data using the active initialization settings. Only selected images are replaced.",
        ConfirmationMessage = "This replaces all catalog, brand, and type rows and updates only images in the selected input set.",
        Arguments =
        [
            new InteractionInput
            {
                Name = "confirmation",
                Label = "Type the confirmation phrase",
                Description = $"Enter exactly: {CatalogResetService.ResetConfirmation}",
                InputType = InputType.Text,
                Required = true
            }
        ]
    });

catalogDb.WithCommand(
    "recover-catalog-reset",
    "Recover interrupted catalog reset",
    RecoverCatalogResetAsync,
    new CommandOptions
    {
        Description = "Restore the catalog database and selected images from the recorded pre-reset snapshot.",
        ConfirmationMessage = "This restores the prior catalog state from the interrupted reset backup.",
        Arguments =
        [
            new InteractionInput
            {
                Name = "confirmation",
                Label = "Type the confirmation phrase",
                Description = $"Enter exactly: {CatalogResetService.RecoveryConfirmation}",
                InputType = InputType.Text,
                Required = true
            }
        ]
    });

builder.AddProject("web", "../eShop.Web/eShop.Web.csproj")
    .WithReference(catalogApi)
    .WithEnvironment("CATALOG_IMAGES_PATH", imageDirectory)
    .WithHttpHealthCheck("/health")
    .WaitFor(catalogApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();

static bool ReadBooleanSetting(string? value, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return bool.TryParse(value, out var result)
        ? result
        : throw new InvalidOperationException("CATALOG_USE_CUSTOMIZATION_DATA must be true or false.");
}

static string ResolveAppHostPath(string path, string appHostDirectory) =>
    Path.GetFullPath(path, appHostDirectory);

async Task<ExecuteCommandResult> ResetCatalogAsync(ExecuteCommandContext context)
{
    if (!string.Equals(
            context.Arguments.GetString("confirmation"),
            CatalogResetService.ResetConfirmation,
            StringComparison.Ordinal))
    {
        return CommandResults.Failure("The required catalog confirmation is missing or does not match.");
    }

    try
    {
        var seedData = useCustomizationData
            ? CatalogCustomizationLoader.LoadDirectory(customizationDirectory)
            : CatalogSeeder.CreateBuiltInSeedData();
        var connectionString = await ((IResourceWithConnectionString)catalogDb.Resource)
            .GetConnectionStringAsync(context.CancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return CommandResults.Failure("The catalog database connection is unavailable.");
        }

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new CatalogDbContext(options);
        var backupRemoved = await CatalogResetService.ResetAsync(
            dbContext,
            seedData,
            imageDirectory,
            CatalogResetService.ResetConfirmation,
            context.CancellationToken);
        return CommandResults.Success(backupRemoved
            ? "Catalog reset completed."
            : "Catalog reset completed, but temporary recovery files remain and require manual cleanup.");
    }
    catch (CatalogCustomizationException exception)
    {
        context.Logger.LogWarning("Catalog reset input validation failed.");
        return CommandResults.Failure(exception.Message);
    }
    catch (CatalogResetException exception)
    {
        context.Logger.LogWarning("Catalog reset was refused or failed.");
        return CommandResults.Failure(exception.Message);
    }
    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
    {
        return CommandResults.Canceled();
    }
    catch (Exception exception)
    {
        context.Logger.LogError("Catalog reset failed ({FailureType}).", exception.GetType().Name);
        return CommandResults.Failure(
            "Catalog reset failed. Catalog operations remain blocked if recovery is required.");
    }
}

async Task<ExecuteCommandResult> RecoverCatalogResetAsync(ExecuteCommandContext context)
{
    if (!string.Equals(
            context.Arguments.GetString("confirmation"),
            CatalogResetService.RecoveryConfirmation,
            StringComparison.Ordinal))
    {
        return CommandResults.Failure("The required recovery confirmation is missing or does not match.");
    }

    try
    {
        var connectionString = await ((IResourceWithConnectionString)catalogDb.Resource)
            .GetConnectionStringAsync(context.CancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return CommandResults.Failure("The catalog database connection is unavailable.");
        }

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new CatalogDbContext(options);
        var backupRemoved = await CatalogResetService.RecoverAsync(
            dbContext,
            imageDirectory,
            CatalogResetService.RecoveryConfirmation,
            context.CancellationToken);
        return CommandResults.Success(backupRemoved
            ? "Catalog recovery completed and the previous catalog state was restored."
            : "Catalog recovery completed, but temporary recovery files remain and require manual cleanup.");
    }
    catch (CatalogResetException exception)
    {
        context.Logger.LogWarning("Catalog recovery was refused or failed.");
        return CommandResults.Failure(exception.Message);
    }
    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
    {
        return CommandResults.Canceled();
    }
    catch (Exception exception)
    {
        context.Logger.LogError("Catalog recovery failed ({FailureType}).", exception.GetType().Name);
        return CommandResults.Failure("Catalog recovery failed; catalog operations remain blocked.");
    }
}
