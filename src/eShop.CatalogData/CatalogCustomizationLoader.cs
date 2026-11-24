using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace eShop.CatalogData;

public sealed record CatalogSeedData(
    IReadOnlyList<CatalogBrand> Brands,
    IReadOnlyList<CatalogType> Types,
    IReadOnlyList<Catalog> Items,
    IReadOnlyDictionary<string, byte[]> Images);

public sealed class CatalogCustomizationException(string message) : Exception(message);

public static class CatalogCustomizationLoader
{
    private const int MaximumCsvBytes = 5 * 1024 * 1024;
    private const int MaximumZipBytes = 64 * 1024 * 1024;
    private const int MaximumImageBytes = 20 * 1024 * 1024;
    private const int MaximumImagesBytes = 100 * 1024 * 1024;
    private const int MaximumImageEntries = 512;
    private const decimal MaximumPrice = 1_000_000m;
    private const int MaximumStock = 10_000_000;

    private static readonly string[] RequiredItemHeaders =
    [
        "CatalogTypeName",
        "CatalogBrandName",
        "Description",
        "Name",
        "Price",
        "PictureFileName"
    ];

    public static CatalogSeedData LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new CatalogCustomizationException("The catalog customization directory does not exist.");
        }

        var types = ReadLookupFile(
            Path.Combine(root, "CatalogTypes.csv"),
            "CatalogType",
            name => new CatalogType { Type = name },
            "catalog type");
        var brands = ReadLookupFile(
            Path.Combine(root, "CatalogBrands.csv"),
            "CatalogBrand",
            name => new CatalogBrand { Brand = name },
            "catalog brand");
        var typeByName = types.ToDictionary(item => item.Type, StringComparer.OrdinalIgnoreCase);
        var brandByName = brands.ToDictionary(item => item.Brand, StringComparer.OrdinalIgnoreCase);
        var archivePath = Path.Combine(root, "CatalogItems.zip");
        var images = ReadImages(archivePath);
        var items = ReadItems(
            Path.Combine(root, "CatalogItems.csv"),
            typeByName,
            brandByName,
            images);

        return new CatalogSeedData(brands, types, items, images);
    }

    private static List<T> ReadLookupFile<T>(
        string path,
        string requiredHeader,
        Func<string, T> create,
        string subject)
    {
        using var parser = CreateCsvParser(path);
        var headers = ReadHeaders(parser, path);
        if (headers.Count != 1
            || !headers.TryGetValue(requiredHeader, out var nameColumn))
        {
            throw new CatalogCustomizationException(
                $"{Path.GetFileName(path)} must contain the '{requiredHeader}' header.");
        }

        var values = new List<T>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!parser.EndOfData)
        {
            var fields = ReadRow(parser, headers.Count, path, values.Count + 2);
            if (fields is null)
            {
                continue;
            }

            var name = Clean(fields[nameColumn]);
            if (name.Length is 0 or > 100 || !names.Add(name))
            {
                throw new CatalogCustomizationException(
                    $"{Path.GetFileName(path)} contains an empty or duplicate {subject}.");
            }

            values.Add(create(name));
        }

        if (values.Count == 0)
        {
            throw new CatalogCustomizationException($"{Path.GetFileName(path)} must contain at least one {subject}.");
        }

        return values;
    }

    private static List<Catalog> ReadItems(
        string path,
        IReadOnlyDictionary<string, CatalogType> typeByName,
        IReadOnlyDictionary<string, CatalogBrand> brandByName,
        IReadOnlyDictionary<string, byte[]> images)
    {
        using var parser = CreateCsvParser(path);
        var headers = ReadHeaders(parser, path);
        var missing = RequiredItemHeaders.Where(header => !headers.ContainsKey(header)).ToArray();
        if (missing.Length > 0)
        {
            throw new CatalogCustomizationException(
                $"{Path.GetFileName(path)} is missing a required column.");
        }

        var items = new List<Catalog>();
        while (!parser.EndOfData)
        {
            var rowNumber = items.Count + 2;
            var fields = ReadRow(parser, headers.Count, path, rowNumber);
            if (fields is null)
            {
                continue;
            }

            var name = Clean(fields[headers["Name"]]);
            var description = Clean(fields[headers["Description"]]);
            var typeName = Clean(fields[headers["CatalogTypeName"]]);
            var brandName = Clean(fields[headers["CatalogBrandName"]]);
            var pictureFileName = Clean(fields[headers["PictureFileName"]]);

            if (name.Length is 0 or > 50)
            {
                throw InvalidItemRow(rowNumber, "Name");
            }

            if (!TryParsePrice(fields[headers["Price"]], out var price))
            {
                throw InvalidItemRow(rowNumber, "Price");
            }

            if (!typeByName.TryGetValue(typeName, out var catalogType))
            {
                throw new CatalogCustomizationException("type=... does not exist in catalogTypes");
            }

            if (!brandByName.TryGetValue(brandName, out var catalogBrand))
            {
                throw new CatalogCustomizationException("type=... does not exist in catalogTypes");
            }

            if (!IsSafeImageFileName(pictureFileName) || !images.ContainsKey(pictureFileName))
            {
                throw InvalidItemRow(rowNumber, "PictureFileName");
            }

            if (!TryReadOptionalInt(fields, OptionalColumn(headers, "AvailableStock"), 0, out var stock)
                || !TryReadOptionalInt(fields, OptionalColumn(headers, "RestockThreshold"), 0, out var restockThreshold)
                || !TryReadOptionalInt(fields, OptionalColumn(headers, "MaxStockThreshold"), 0, out var maxStockThreshold))
            {
                throw InvalidItemRow(rowNumber, "inventory");
            }

            var onReorder = false;
            if (OptionalColumn(headers, "OnReorder") is { } onReorderColumn
                && !string.IsNullOrWhiteSpace(fields[onReorderColumn])
                && !bool.TryParse(Clean(fields[onReorderColumn]), out onReorder))
            {
                throw InvalidItemRow(rowNumber, "OnReorder");
            }

            items.Add(new Catalog
            {
                Name = name,
                Description = description,
                Price = price,
                PictureFileName = pictureFileName,
                AvailableStock = stock,
                RestockThreshold = restockThreshold,
                MaxStockThreshold = maxStockThreshold,
                OnReorder = onReorder,
                CatalogType = catalogType,
                CatalogBrand = catalogBrand
            });
        }

        if (items.Count == 0)
        {
            throw new CatalogCustomizationException($"{Path.GetFileName(path)} must contain at least one product.");
        }

        return items;
    }

    private static TextFieldParser CreateCsvParser(string path)
    {
        if (!File.Exists(path))
        {
            throw new CatalogCustomizationException($"{Path.GetFileName(path)} is required but was not found.");
        }

        try
        {
            if (new FileInfo(path).Length > MaximumCsvBytes)
            {
                throw new CatalogCustomizationException($"{Path.GetFileName(path)} exceeds the allowed size.");
            }

            var parser = new TextFieldParser(path, Encoding.UTF8, detectEncoding: true)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");
            return parser;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogCustomizationException($"{Path.GetFileName(path)} could not be read.");
        }
    }

    private static Dictionary<string, int> ReadHeaders(TextFieldParser parser, string path)
    {
        try
        {
            if (parser.EndOfData || parser.ReadFields() is not { Length: > 0 } fields)
            {
                throw new CatalogCustomizationException($"{Path.GetFileName(path)} has no header row.");
            }

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < fields.Length; index++)
            {
                var header = Clean(fields[index]);
                if (header.Length == 0 || !headers.TryAdd(header, index))
                {
                    throw new CatalogCustomizationException(
                        $"{Path.GetFileName(path)} contains an empty or duplicate header.");
                }
            }

            return headers;
        }
        catch (MalformedLineException)
        {
            throw new CatalogCustomizationException($"{Path.GetFileName(path)} contains an invalid CSV header.");
        }
    }

    private static string[]? ReadRow(TextFieldParser parser, int fieldCount, string path, int rowNumber)
    {
        try
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                return null;
            }

            if (fields.Length != fieldCount)
            {
                throw InvalidItemRow(rowNumber, "column count");
            }

            return fields;
        }
        catch (MalformedLineException)
        {
            throw new CatalogCustomizationException(
                $"{Path.GetFileName(path)} contains an invalid CSV row ({rowNumber}).");
        }
    }

    private static Dictionary<string, byte[]> ReadImages(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            throw new CatalogCustomizationException("CatalogItems.zip is required but was not found.");
        }

        try
        {
            if (new FileInfo(archivePath).Length > MaximumZipBytes)
            {
                throw new CatalogCustomizationException("CatalogItems.zip exceeds the allowed size.");
            }

            using var stream = File.OpenRead(archivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count is 0 or > MaximumImageEntries)
            {
                throw new CatalogCustomizationException("CatalogItems.zip has an invalid number of entries.");
            }

            var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                var segments = entryPath.Split('/');
                var pathSegments = entryPath.EndsWith("/", StringComparison.Ordinal)
                    ? segments[..^1]
                    : segments;
                if (entryPath.StartsWith('/', StringComparison.Ordinal)
                    || entryPath.Contains(':')
                    || pathSegments.Any(segment => segment.Length == 0)
                    || pathSegments.Any(segment => segment is "." or ".."))
                {
                    throw new CatalogCustomizationException("CatalogItems.zip contains an unsafe path.");
                }

                if (entry.Name.Length == 0)
                {
                    continue;
                }

                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                {
                    throw new CatalogCustomizationException("CatalogItems.zip contains an unsupported link entry.");
                }

                var fileName = segments[^1];
                if (!IsSafeImageFileName(fileName)
                    || entry.Length is <= 0 or > MaximumImageBytes
                    || !images.TryAdd(fileName, []))
                {
                    throw new CatalogCustomizationException("CatalogItems.zip contains an invalid or duplicate image.");
                }

                totalBytes += entry.Length;
                if (totalBytes > MaximumImagesBytes)
                {
                    throw new CatalogCustomizationException("CatalogItems.zip exceeds the total image size limit.");
                }

                var content = ReadEntry(entry);
                if (!HasImageSignature(fileName, content))
                {
                    throw new CatalogCustomizationException("CatalogItems.zip contains a file that is not a supported image.");
                }

                images[fileName] = content;
            }

            if (images.Count == 0)
            {
                throw new CatalogCustomizationException("CatalogItems.zip contains no image files.");
            }

            return images;
        }
        catch (InvalidDataException)
        {
            throw new CatalogCustomizationException("CatalogItems.zip is invalid.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogCustomizationException("CatalogItems.zip could not be read.");
        }
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaximumImageBytes)
            {
                throw new CatalogCustomizationException("CatalogItems.zip contains an oversized image.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static bool TryParsePrice(string value, out decimal price) =>
        decimal.TryParse(
            Clean(value),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out price)
        && price is >= 0 and <= MaximumPrice
        && decimal.Round(price, 2) == price;

    private static bool TryReadOptionalInt(
        string[] fields,
        int? column,
        int defaultValue,
        out int value)
    {
        value = defaultValue;
        if (column is null || string.IsNullOrWhiteSpace(fields[column.Value]))
        {
            return true;
        }

        return int.TryParse(
                Clean(fields[column.Value]),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
            && value is >= 0 and <= MaximumStock;
    }

    private static string Clean(string value) => value.Trim();

    private static int? OptionalColumn(IReadOnlyDictionary<string, int> headers, string name) =>
        headers.TryGetValue(name, out var column) ? column : null;

    private static bool IsSafeImageFileName(string name) =>
        name.Length > 0
        && name is not "." and not ".."
        && string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
        && name.IndexOfAny(['/', '\\', ':', '\0']) < 0
        && Path.GetExtension(name).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";

    private static bool HasImageSignature(string fileName, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => bytes.Length >= 8
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytes.Length >= 4
                && bytes[0] == 0xFF && bytes[1] == 0xD8
                && bytes[^2] == 0xFF && bytes[^1] == 0xD9,
            ".gif" => bytes.Length >= 6
                && (Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a"),
            ".webp" => bytes.Length >= 12
                && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            _ => false
        };
    }

    private static CatalogCustomizationException InvalidItemRow(int rowNumber, string field) =>
        new($"CatalogItems.csv contains an invalid {field} value at row {rowNumber}.");
}
