using System.IO.Compression;
using System.Text;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace eShop.CatalogApi.Tests;

public sealed class CatalogCustomizationInitializationTests(PostgreSqlContainer container)
    : CatalogTestBase(container)
{
    protected override bool InitializeDatabase => false;

    [Fact]
    public async Task InitializeConfiguredAsync_InvalidFirstRunInputDoesNotMigrateOrCreateImages()
    {
        await using var db = CreateDbContext();
        var imageRoot = Path.Combine(Path.GetTempPath(), $"catalog-init-{Guid.NewGuid():N}");

        await Assert.ThrowsAsync<CatalogCustomizationException>(() =>
            CatalogSeeder.InitializeConfiguredAsync(
                db,
                useCustomizationData: true,
                customizationDirectory: Path.Combine(imageRoot, "missing-input"),
                imageDirectory: imageRoot));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL",
            connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
        Assert.False(Directory.Exists(imageRoot));
    }

    [Fact]
    public async Task InitializeConfiguredAsync_ImageConflictDoesNotMigrateOrOverwriteExistingImage()
    {
        await using var db = CreateDbContext();
        var inputDirectory = CreateInputDirectory();
        var testWorkspace = Path.Combine(Path.GetTempPath(), $"catalog-init-conflict-{Guid.NewGuid():N}");
        var imageRoot = Path.Combine(testWorkspace, "images");
        Directory.CreateDirectory(imageRoot);
        var existingBytes = new byte[] { 1, 2, 3, 4 };
        var existingImage = Path.Combine(imageRoot, "product.png");
        await File.WriteAllBytesAsync(existingImage, existingBytes);

        try
        {
            await Assert.ThrowsAsync<CatalogCustomizationException>(() =>
                CatalogSeeder.InitializeConfiguredAsync(
                    db,
                    useCustomizationData: true,
                    customizationDirectory: inputDirectory,
                    imageDirectory: imageRoot));

            Assert.Equal(existingBytes, await File.ReadAllBytesAsync(existingImage));
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL",
                connection);
            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(testWorkspace, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeConfiguredAsync_InitializedDatabaseIgnoresChangedInvalidInputAndPreservesData()
    {
        var inputDirectory = CreateInputDirectory();
        var imageRoot = Path.Combine(Path.GetTempPath(), $"catalog-init-images-{Guid.NewGuid():N}");

        try
        {
            await using (var db = CreateDbContext())
            {
                await CatalogSeeder.InitializeConfiguredAsync(
                    db,
                    useCustomizationData: true,
                    customizationDirectory: inputDirectory,
                    imageDirectory: imageRoot);
                var item = await db.Catalogs.SingleAsync();
                Assert.Equal("Custom product", item.Name);
                item.Name = "User-edited product";
                await db.SaveChangesAsync();
            }

            await using (var db = CreateDbContext())
            {
                await CatalogSeeder.InitializeConfiguredAsync(
                    db,
                    useCustomizationData: true,
                    customizationDirectory: Path.Combine(imageRoot, "missing-input"),
                    imageDirectory: imageRoot);
                Assert.Equal("User-edited product", (await db.Catalogs.SingleAsync()).Name);
            }

            Assert.True(File.Exists(Path.Combine(imageRoot, "product.png")));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            if (Directory.Exists(imageRoot))
            {
                Directory.Delete(imageRoot, recursive: true);
            }
        }
    }

    private static string CreateInputDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"catalog-custom-data-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "CatalogBrands.csv"),
            "CatalogBrand\nCustom brand\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(directory, "CatalogTypes.csv"),
            "CatalogType\nCustom type\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(directory, "CatalogItems.csv"),
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName\nCustom type,Custom brand,Custom description,Custom product,12.34,product.png\n",
            Encoding.UTF8);
        using var archive = ZipFile.Open(
            Path.Combine(directory, "CatalogItems.zip"),
            ZipArchiveMode.Create);
        var entry = archive.CreateEntry("product.png");
        using var output = entry.Open();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3]);
        return directory;
    }
}
