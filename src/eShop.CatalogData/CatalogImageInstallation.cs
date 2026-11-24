using System.Security.Cryptography;

namespace eShop.CatalogData;

public sealed class CatalogImageInstallation : IDisposable
{
    private readonly string targetDirectory;
    private readonly string stagingDirectory;
    private bool committed;

    internal CatalogImageInstallation(
        string targetDirectory,
        string stagingDirectory,
        IReadOnlyDictionary<string, string> hashes)
    {
        this.targetDirectory = targetDirectory;
        this.stagingDirectory = stagingDirectory;
        Hashes = hashes;
    }

    public IReadOnlyDictionary<string, string> Hashes { get; }

    public void Commit() => committed = true;

    public void Dispose()
    {
        try
        {
            if (!committed)
            {
                foreach (var (fileName, hash) in Hashes)
                {
                    var path = Path.Combine(targetDirectory, fileName);
                    if (File.Exists(path) && CatalogImageInstallationStore.GetHash(path) == hash)
                    {
                        File.Delete(path);
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }
}

public static class CatalogImageInstallationStore
{
    public static CatalogImageInstallation InstallNewFiles(
        string imageDirectory,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);
        ArgumentNullException.ThrowIfNull(images);

        try
        {
            var target = Path.GetFullPath(imageDirectory);
            ValidateNoConflicts(target, images);
            Directory.CreateDirectory(target);
            ValidateNoConflicts(target, images);

            var staging = Path.Combine(target, $".catalog-stage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staging);
            var installedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var (fileName, content) in images)
                {
                    if (content.Length == 0)
                    {
                        throw new CatalogCustomizationException("An image file is empty.");
                    }

                    var stagedPath = Path.Combine(staging, fileName);
                    using (var output = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        output.Write(content);
                        output.Flush(flushToDisk: true);
                    }
                }

                foreach (var (fileName, content) in images)
                {
                    var stagedPath = Path.Combine(staging, fileName);
                    var targetPath = Path.Combine(target, fileName);
                    File.Move(stagedPath, targetPath);
                    installedHashes.Add(fileName, GetHash(content));
                }

                return new CatalogImageInstallation(target, staging, installedHashes);
            }
            catch (Exception exception) when (IsFileOperationException(exception))
            {
                try
                {
                    Rollback(target, installedHashes);
                    Directory.Delete(staging, recursive: true);
                }
                catch (Exception cleanupException) when (IsFileOperationException(cleanupException))
                {
                    throw new CatalogCustomizationException(
                        "Catalog image installation failed and partial files could not be fully rolled back.");
                }

                if (exception is CatalogCustomizationException customizationException)
                {
                    throw customizationException;
                }

                throw new CatalogCustomizationException("Catalog image installation failed.");
            }
        }
        catch (Exception exception) when (IsFileOperationException(exception))
        {
            if (exception is CatalogCustomizationException customizationException)
            {
                throw customizationException;
            }

            throw new CatalogCustomizationException("Catalog image directory could not be prepared.");
        }
    }

    internal static string GetHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string GetHash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));

    internal static void ValidateImageDirectory(string imageDirectory)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(imageDirectory));
             current is not null;
             current = current.Parent)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new CatalogCustomizationException("The catalog image directory contains an unsupported link.");
            }
        }
    }

    public static void ValidateNoConflicts(
        string imageDirectory,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);
        ArgumentNullException.ThrowIfNull(images);
        var target = Path.GetFullPath(imageDirectory);
        ValidateImageDirectory(target);
        if (!Directory.Exists(target))
        {
            return;
        }

        var existingNames = Directory.EnumerateFileSystemEntries(target)
            .Select(path => Path.GetFileName(path)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (images.Keys.Any(name => !IsSafeFileName(name) || existingNames.Contains(name)))
        {
            throw new CatalogCustomizationException(
                "The image directory contains a conflicting or unsafe image file name.");
        }
    }

    private static bool IsSafeFileName(string name) =>
        name.Length > 0
        && name is not "." and not ".."
        && string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
        && name.IndexOfAny(['/', '\\', ':', '\0']) < 0;

    private static void Rollback(string target, IReadOnlyDictionary<string, string> hashes)
    {
        foreach (var (fileName, hash) in hashes)
        {
            var path = Path.Combine(target, fileName);
            if (File.Exists(path) && GetHash(path) == hash)
            {
                File.Delete(path);
            }
        }
    }

    private static bool IsFileOperationException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or CatalogCustomizationException;
}
