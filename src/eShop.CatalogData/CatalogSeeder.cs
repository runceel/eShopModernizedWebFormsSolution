using Microsoft.EntityFrameworkCore;

namespace eShop.CatalogData;

public static class CatalogSeeder
{
    private const long InitializationLockId = 773219843501L;

    public static async Task InitializeAsync(
        CatalogDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({InitializationLockId})",
            cancellationToken);

        var markers = await dbContext.CatalogInitializations
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (markers.Count > 0)
        {
            if (markers.Count != 1 || markers[0].Id != 1)
            {
                throw new InvalidOperationException("The catalog initialization marker is invalid.");
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (await dbContext.Catalogs.AnyAsync(cancellationToken)
            || await dbContext.CatalogBrands.AnyAsync(cancellationToken)
            || await dbContext.CatalogTypes.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Catalog rows exist without an initialization marker; refusing to seed an unverified database.");
        }

        var brands = new[]
        {
            new CatalogBrand { Id = 1, Brand = "Azure" },
            new CatalogBrand { Id = 2, Brand = ".NET" },
            new CatalogBrand { Id = 3, Brand = "Visual Studio" },
            new CatalogBrand { Id = 4, Brand = "SQL Server" },
            new CatalogBrand { Id = 5, Brand = "Other" }
        };
        var types = new[]
        {
            new CatalogType { Id = 1, Type = "Mug" },
            new CatalogType { Id = 2, Type = "T-Shirt" },
            new CatalogType { Id = 3, Type = "Sheet" },
            new CatalogType { Id = 4, Type = "USB Memory Stick" }
        };

        dbContext.CatalogBrands.AddRange(brands);
        dbContext.CatalogTypes.AddRange(types);
        dbContext.Catalogs.AddRange(
            new Catalog { Id = 1, Name = ".NET Bot Black Hoodie", Description = ".NET Bot Black Hoodie", Price = 19.50m, PictureFileName = "1.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 2, Name = ".NET Black & White Mug", Description = ".NET Black & White Mug", Price = 8.50m, PictureFileName = "2.png", CatalogTypeId = 1, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 3, Name = "Prism White T-Shirt", Description = "Prism White T-Shirt", Price = 12.00m, PictureFileName = "3.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100 },
            new Catalog { Id = 4, Name = ".NET Foundation T-shirt", Description = ".NET Foundation T-shirt", Price = 12.00m, PictureFileName = "4.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 5, Name = "Roslyn Red Sheet", Description = "Roslyn Red Sheet", Price = 8.50m, PictureFileName = "5.png", CatalogTypeId = 3, CatalogBrandId = 5, AvailableStock = 100 },
            new Catalog { Id = 6, Name = ".NET Blue Hoodie", Description = ".NET Blue Hoodie", Price = 12.00m, PictureFileName = "6.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 7, Name = "Roslyn Red T-Shirt", Description = "Roslyn Red T-Shirt", Price = 12.00m, PictureFileName = "7.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100 },
            new Catalog { Id = 8, Name = "Kudu Purple Hoodie", Description = "Kudu Purple Hoodie", Price = 8.50m, PictureFileName = "8.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100 },
            new Catalog { Id = 9, Name = "Cup<T> White Mug", Description = "Cup<T> White Mug", Price = 12.00m, PictureFileName = "9.png", CatalogTypeId = 1, CatalogBrandId = 5, AvailableStock = 100 },
            new Catalog { Id = 10, Name = ".NET Foundation Sheet", Description = ".NET Foundation Sheet", Price = 12.00m, PictureFileName = "10.png", CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 11, Name = "Cup<T> Sheet", Description = "Cup<T> Sheet", Price = 8.50m, PictureFileName = "11.png", CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100 },
            new Catalog { Id = 12, Name = "Prism White TShirt", Description = "Prism White TShirt", Price = 12.00m, PictureFileName = "12.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100 });

        await dbContext.SaveChangesAsync(cancellationToken);
        await AlignIdentitySequencesAsync(dbContext, cancellationToken);
        dbContext.CatalogInitializations.Add(new CatalogInitialization
        {
            Id = 1,
            InitializedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AlignIdentitySequencesAsync(
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('catalog', 'id'), (SELECT MAX(id) FROM catalog), true)",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('catalog_brand', 'id'), (SELECT MAX(id) FROM catalog_brand), true)",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('catalog_type', 'id'), (SELECT MAX(id) FROM catalog_type), true)",
            cancellationToken);
    }
}
