using System.Security.Cryptography;
using System.Text.Json;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShop.CatalogApi.Tests;

public sealed class CatalogResetTests(PostgreSqlContainer container) : CatalogTestBase(container)
{
    [Fact]
    public async Task ResetAsync_RejectsMissingConfirmationWithoutChangingCatalog()
    {
        await using var db = CreateDbContext();

        var exception = await Assert.ThrowsAsync<CatalogResetException>(() =>
            CatalogResetService.ResetAsync(
                db,
                CatalogSeeder.CreateBuiltInSeedData(),
                Path.Combine(Path.GetTempPath(), $"catalog-reset-{Guid.NewGuid():N}"),
                string.Empty));

        Assert.Contains("confirmation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, await db.Catalogs.CountAsync());
        Assert.Equal("initialized", (await db.CatalogInitializations.SingleAsync()).Status);
    }

    [Fact]
    public async Task ResetAsync_RestoresDatabaseWhenReplacementFails()
    {
        await using var db = CreateDbContext();
        var invalidSeed = new CatalogSeedData(
            [],
            [],
            [
                new Catalog
                {
                    Name = "Invalid replacement",
                    Description = "Invalid replacement",
                    Price = 1m,
                    PictureFileName = "missing.png",
                    CatalogBrandId = int.MaxValue,
                    CatalogTypeId = int.MaxValue
                }
            ],
            new Dictionary<string, byte[]>());

        var exception = await Assert.ThrowsAsync<CatalogResetException>(() =>
            CatalogResetService.ResetAsync(
                db,
                invalidSeed,
                Path.Combine(Path.GetTempPath(), $"catalog-reset-{Guid.NewGuid():N}"),
                CatalogResetService.ResetConfirmation));

        Assert.Contains("previous catalog state was restored", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, await db.Catalogs.CountAsync());
        Assert.Equal(5, await db.CatalogBrands.CountAsync());
        Assert.Equal(4, await db.CatalogTypes.CountAsync());
        var marker = await db.CatalogInitializations.SingleAsync();
        Assert.Equal("initialized", marker.Status);
        Assert.Null(marker.ResetOperationId);
    }

    [Fact]
    public async Task ResetAsync_UpdatesOnlySelectedImagesAndPreservesUnselectedFiles()
    {
        await using var db = CreateDbContext();
        var testWorkspace = Path.Combine(Path.GetTempPath(), $"catalog-reset-{Guid.NewGuid():N}");
        var imageRoot = Path.Combine(testWorkspace, "images");
        Directory.CreateDirectory(testWorkspace);
        Directory.CreateDirectory(imageRoot);
        var keepImage = Path.Combine(imageRoot, "keep.png");
        byte[] keepBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
        await File.WriteAllBytesAsync(keepImage, keepBytes);

        try
        {
            byte[] firstImage = [137, 80, 78, 71, 13, 10, 26, 10, 4, 5, 6];
            var firstReset = await CatalogResetService.ResetAsync(
                db,
                CreateCustomSeed("First product", firstImage),
                imageRoot,
                CatalogResetService.ResetConfirmation);

            Assert.True(firstReset);
            Assert.Equal(firstImage, await File.ReadAllBytesAsync(Path.Combine(imageRoot, "product.png")));
            Assert.Equal(keepBytes, await File.ReadAllBytesAsync(keepImage));
            Assert.Equal("First product", (await db.Catalogs.SingleAsync()).Name);

            byte[] replacementImage = [137, 80, 78, 71, 13, 10, 26, 10, 7, 8, 9];
            var secondReset = await CatalogResetService.ResetAsync(
                db,
                CreateCustomSeed("Updated product", replacementImage),
                imageRoot,
                CatalogResetService.ResetConfirmation);

            Assert.True(secondReset);
            Assert.Equal(replacementImage, await File.ReadAllBytesAsync(Path.Combine(imageRoot, "product.png")));
            Assert.Equal(keepBytes, await File.ReadAllBytesAsync(keepImage));
            Assert.Equal("Updated product", (await db.Catalogs.SingleAsync()).Name);
            var managedHashes = JsonSerializer.Deserialize<Dictionary<string, string>>(
                (await db.CatalogInitializations.SingleAsync()).ManagedImageHashesJson);
            Assert.NotNull(managedHashes);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(replacementImage)),
                managedHashes["product.png"]);
        }
        finally
        {
            Directory.Delete(testWorkspace, recursive: true);
        }
    }

    [Fact]
    public async Task RecoverAsync_RestoresPersistedSnapshotAndKeepsApiUnavailableUntilComplete()
    {
        await using var db = CreateDbContext();
        var imageRoot = Path.Combine(Path.GetTempPath(), $"catalog-recovery-{Guid.NewGuid():N}");
        var marker = await db.CatalogInitializations.SingleAsync();
        var snapshot = new
        {
            marker.InitializedAtUtc,
            marker.ManagedImageHashesJson,
            Brands = await db.CatalogBrands.AsNoTracking().Select(item => new { item.Id, item.Brand }).ToListAsync(),
            Types = await db.CatalogTypes.AsNoTracking().Select(item => new { item.Id, item.Type }).ToListAsync(),
            Items = await db.Catalogs.AsNoTracking().Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Price,
                item.PictureFileName,
                item.CatalogTypeId,
                item.CatalogBrandId,
                item.AvailableStock,
                item.RestockThreshold,
                item.MaxStockThreshold,
                item.OnReorder
            }).ToListAsync()
        };
        marker.Status = "resetting";
        marker.ResetOperationId = Guid.NewGuid();
        marker.ResetStartedAtUtc = DateTimeOffset.UtcNow;
        marker.ResetSnapshotJson = JsonSerializer.Serialize(snapshot);
        marker.ResetImageDirectory = Path.GetFullPath(imageRoot);
        marker.ResetImageManifestJson = "[]";
        await db.SaveChangesAsync();

        using var health = await Client.GetAsync("/health");
        using var operations = await Client.GetAsync("/api/catalog");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, operations.StatusCode);

        var backupRemoved = await CatalogResetService.RecoverAsync(
            db,
            imageRoot,
            CatalogResetService.RecoveryConfirmation);

        Assert.True(backupRemoved);
        Assert.Equal(12, await db.Catalogs.CountAsync());
        Assert.Equal("initialized", (await db.CatalogInitializations.SingleAsync()).Status);
        using var recoveredHealth = await Client.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, recoveredHealth.StatusCode);
    }

    private static CatalogSeedData CreateCustomSeed(string productName, byte[] image)
    {
        var brand = new CatalogBrand { Brand = "Custom brand" };
        var type = new CatalogType { Type = "Custom type" };
        var item = new Catalog
        {
            Name = productName,
            Description = productName,
            Price = 12.34m,
            PictureFileName = "product.png",
            CatalogBrand = brand,
            CatalogType = type
        };
        return new CatalogSeedData(
            [brand],
            [type],
            [item],
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["product.png"] = image
            });
    }
}
