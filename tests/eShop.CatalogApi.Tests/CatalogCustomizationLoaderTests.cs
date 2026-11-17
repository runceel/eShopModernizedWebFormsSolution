using System.IO.Compression;
using System.Text;
using eShop.CatalogData;
using Xunit;

namespace eShop.CatalogApi.Tests;

public sealed class CatalogCustomizationLoaderTests
{
    [Fact]
    public void LoadDirectory_AcceptsCurrentCsvFormatAndDefaultsMissingOptionalColumns()
    {
        var directory = CreateInputDirectory(
            "catalogbrand\nAcme\n",
            "catalogtype\nMug\n",
            "catalogtypename,catalogbrandname,description,name,price,picturefilename\nMug,Acme,Description,Product,19.50,product.png\n",
            ("images/product.png", PngSignature));

        try
        {
            var data = CatalogCustomizationLoader.LoadDirectory(directory);

            var item = Assert.Single(data.Items);
            Assert.Equal(19.50m, item.Price);
            Assert.Equal("Acme", item.CatalogBrand.Brand);
            Assert.Equal("Mug", item.CatalogType.Type);
            Assert.Equal(0, item.AvailableStock);
            Assert.Equal(0, item.RestockThreshold);
            Assert.Equal(0, item.MaxStockThreshold);
            Assert.False(item.OnReorder);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_RejectsCultureSpecificPriceWithoutLeakingInputValue()
    {
        var directory = CreateInputDirectory(
            "CatalogBrand\nAcme\n",
            "CatalogType\nMug\n",
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName\nMug,Acme,Description,Product,\"19,50\",product.png\n",
            ("product.png", PngSignature));

        try
        {
            var exception = Assert.Throws<CatalogCustomizationException>(
                () => CatalogCustomizationLoader.LoadDirectory(directory));

            Assert.Contains("Price", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("19,50", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_RejectsUnknownBrandWithCompatibilityDiagnostic()
    {
        var directory = CreateInputDirectory(
            "CatalogBrand\nAcme\n",
            "CatalogType\nMug\n",
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName\nMug,SecretBrand,Description,Product,19.50,product.png\n",
            ("product.png", PngSignature));

        try
        {
            var exception = Assert.Throws<CatalogCustomizationException>(
                () => CatalogCustomizationLoader.LoadDirectory(directory));

            Assert.Equal("type=... does not exist in catalogTypes", exception.Message);
            Assert.DoesNotContain("SecretBrand", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_RejectsZipTraversalBeforeReturningSeedData()
    {
        var directory = CreateInputDirectory(
            "CatalogBrand\nAcme\n",
            "CatalogType\nMug\n",
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName\nMug,Acme,Description,Product,19.50,product.png\n",
            ("../product.png", PngSignature));

        try
        {
            var exception = Assert.Throws<CatalogCustomizationException>(
                () => CatalogCustomizationLoader.LoadDirectory(directory));

            Assert.Contains("unsafe path", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_RejectsReferencedImageWithUnsupportedContent()
    {
        var directory = CreateInputDirectory(
            "CatalogBrand\nAcme\n",
            "CatalogType\nMug\n",
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName\nMug,Acme,Description,Product,19.50,product.png\n",
            ("product.png", Encoding.UTF8.GetBytes("not an image")));

        try
        {
            var exception = Assert.Throws<CatalogCustomizationException>(
                () => CatalogCustomizationLoader.LoadDirectory(directory));

            Assert.Contains("not a supported image", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private static string CreateInputDirectory(
        string brandsCsv,
        string typesCsv,
        string itemsCsv,
        params (string Name, byte[] Content)[] archiveEntries)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"catalog-customization-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "CatalogBrands.csv"), brandsCsv, Encoding.UTF8);
        File.WriteAllText(Path.Combine(directory, "CatalogTypes.csv"), typesCsv, Encoding.UTF8);
        File.WriteAllText(Path.Combine(directory, "CatalogItems.csv"), itemsCsv, Encoding.UTF8);
        using var archive = ZipFile.Open(
            Path.Combine(directory, "CatalogItems.zip"),
            ZipArchiveMode.Create);
        foreach (var (name, content) in archiveEntries)
        {
            var entry = archive.CreateEntry(name);
            using var output = entry.Open();
            output.Write(content);
        }

        return directory;
    }
}
