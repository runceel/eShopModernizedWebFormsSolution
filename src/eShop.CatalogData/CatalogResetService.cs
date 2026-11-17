using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace eShop.CatalogData;

public sealed class CatalogResetException(string message) : Exception(message);

public static class CatalogResetService
{
    public const string ResetConfirmation = "RESET CATALOG AND SELECTED IMAGES";
    public const string RecoveryConfirmation = "RESTORE PREVIOUS CATALOG";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<bool> ResetAsync(
        CatalogDbContext dbContext,
        CatalogSeedData seedData,
        string imageDirectory,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ValidateConfirmation(confirmation, ResetConfirmation);
        ArgumentNullException.ThrowIfNull(seedData);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);

        await dbContext.Database.MigrateAsync(cancellationToken);
        var imageRoot = Path.GetFullPath(imageDirectory);
        var operationId = Guid.NewGuid();
        DatabaseSnapshot snapshot;
        List<ResetImageEntry> imageEntries = [];
        string? backupDirectory = null;
        Dictionary<string, string> managedImages;
        var commitAttempted = false;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LockCatalogAsync(dbContext, cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                "LOCK TABLE catalog, catalog_brand, catalog_type IN EXCLUSIVE MODE",
                cancellationToken);
            var marker = await dbContext.CatalogInitializations
                .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
            if (marker is null
                || marker.Status != "initialized"
                || marker.ResetOperationId is not null
                || await dbContext.CatalogInitializations.CountAsync(cancellationToken) != 1)
            {
                throw new CatalogResetException("The catalog changed while reset was being prepared.");
            }

            snapshot = await CaptureSnapshotAsync(dbContext, marker, cancellationToken);
            managedImages = DeserializeManagedImages(marker.ManagedImageHashesJson);
            (backupDirectory, imageEntries) = PrepareImageBackup(
                imageRoot,
                operationId,
                seedData.Images,
                managedImages);

            marker.Status = "resetting";
            marker.ResetOperationId = operationId;
            marker.ResetStartedAtUtc = DateTimeOffset.UtcNow;
            marker.ResetSnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            marker.ResetImageDirectory = imageRoot;
            marker.ResetBackupDirectory = backupDirectory;
            marker.ResetImageManifestJson = JsonSerializer.Serialize(imageEntries, JsonOptions);
            await dbContext.SaveChangesAsync(cancellationToken);
            commitAttempted = true;
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (!commitAttempted
                && backupDirectory is not null
                && !TryDeleteDirectory(backupDirectory))
            {
                throw new CatalogResetException(
                    "Catalog reset preparation failed and temporary recovery files could not be removed.");
            }

            throw;
        }

        try
        {
            await ReplaceDatabaseAsync(dbContext, seedData, cancellationToken);
            ApplyReplacementImages(imageRoot, backupDirectory, imageEntries);
            var newManagedImages = new Dictionary<string, string>(managedImages, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in imageEntries)
            {
                newManagedImages[entry.Name] = entry.NewHash;
            }

            await CompleteOperationAsync(
                dbContext,
                operationId,
                JsonSerializer.Serialize(newManagedImages, JsonOptions),
                null,
                cancellationToken);
        }
        catch
        {
            try
            {
                await EnsureResetPendingAsync(
                    dbContext,
                    operationId,
                    imageRoot,
                    backupDirectory,
                    snapshot,
                    imageEntries,
                    CancellationToken.None);
                await RestoreDatabaseAsync(dbContext, snapshot, CancellationToken.None);
                RestoreImages(imageRoot, backupDirectory, imageEntries);
                await CompleteOperationAsync(
                    dbContext,
                    operationId,
                    snapshot.ManagedImageHashesJson,
                    snapshot.InitializedAtUtc,
                    CancellationToken.None);
            }
            catch
            {
                throw new CatalogResetException(
                    "Catalog reset failed; recovery is required before catalog access can resume.");
            }

            var backupRemoved = TryDeleteDirectory(backupDirectory);
            throw new CatalogResetException(backupRemoved
                ? "Catalog reset failed; the previous catalog state was restored."
                : "Catalog reset failed; the previous state was restored, but temporary recovery files remain.");
        }

        return TryDeleteDirectory(backupDirectory);
    }

    public static async Task<bool> RecoverAsync(
        CatalogDbContext dbContext,
        string imageDirectory,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ValidateConfirmation(confirmation, RecoveryConfirmation);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);

        await dbContext.Database.MigrateAsync(cancellationToken);
        var marker = await GetResettingMarkerAsync(dbContext, cancellationToken);
        if (marker.ResetOperationId is not { } operationId
            || marker.ResetSnapshotJson is null
            || marker.ResetImageManifestJson is null
            || marker.ResetImageDirectory is null)
        {
            throw new CatalogResetException("No recoverable catalog reset is recorded.");
        }

        var imageRoot = Path.GetFullPath(imageDirectory);
        if (!string.Equals(imageRoot, marker.ResetImageDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new CatalogResetException(
                "The configured image directory does not match the interrupted reset; restore the original setting before recovery.");
        }

        var backupDirectory = GetBackupDirectory(imageRoot, operationId);
        if (!string.Equals(
                Path.GetFullPath(marker.ResetBackupDirectory ?? backupDirectory),
                backupDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new CatalogResetException("The recorded catalog reset backup is invalid.");
        }

        var snapshot = DeserializeSnapshot(marker.ResetSnapshotJson);
        var imageEntries = DeserializeImageEntries(marker.ResetImageManifestJson);
        await RestoreDatabaseAsync(dbContext, snapshot, cancellationToken);
        RestoreImages(imageRoot, marker.ResetBackupDirectory, imageEntries);
        await CompleteOperationAsync(
            dbContext,
            operationId,
            snapshot.ManagedImageHashesJson,
            snapshot.InitializedAtUtc,
            cancellationToken);

        return TryDeleteDirectory(marker.ResetBackupDirectory);
    }

    private static async Task<CatalogInitialization> GetResettingMarkerAsync(
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockCatalogAsync(dbContext, cancellationToken);
        var marker = await dbContext.CatalogInitializations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (marker is null
            || marker.Status != "resetting"
            || marker.ResetOperationId is null)
        {
            throw new CatalogResetException("No interrupted catalog reset requires recovery.");
        }

        return marker;
    }

    private static async Task<DatabaseSnapshot> CaptureSnapshotAsync(
        CatalogDbContext dbContext,
        CatalogInitialization marker,
        CancellationToken cancellationToken)
    {
        var brands = await dbContext.CatalogBrands
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new BrandSnapshot { Id = item.Id, Brand = item.Brand })
            .ToListAsync(cancellationToken);
        var types = await dbContext.CatalogTypes
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new TypeSnapshot { Id = item.Id, Type = item.Type })
            .ToListAsync(cancellationToken);
        var items = await dbContext.Catalogs
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new ItemSnapshot
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                PictureFileName = item.PictureFileName,
                CatalogTypeId = item.CatalogTypeId,
                CatalogBrandId = item.CatalogBrandId,
                AvailableStock = item.AvailableStock,
                RestockThreshold = item.RestockThreshold,
                MaxStockThreshold = item.MaxStockThreshold,
                OnReorder = item.OnReorder
            })
            .ToListAsync(cancellationToken);

        _ = DeserializeManagedImages(marker.ManagedImageHashesJson);
        return new DatabaseSnapshot
        {
            InitializedAtUtc = marker.InitializedAtUtc,
            ManagedImageHashesJson = marker.ManagedImageHashesJson,
            Brands = brands,
            Types = types,
            Items = items
        };
    }

    private static async Task ReplaceDatabaseAsync(
        CatalogDbContext dbContext,
        CatalogSeedData seedData,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockCatalogAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog_brand", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog_type", cancellationToken);

        dbContext.CatalogBrands.AddRange(seedData.Brands);
        dbContext.CatalogTypes.AddRange(seedData.Types);
        dbContext.Catalogs.AddRange(seedData.Items);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CatalogSeeder.AlignIdentitySequencesAsync(dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task RestoreDatabaseAsync(
        CatalogDbContext dbContext,
        DatabaseSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockCatalogAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog_brand", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM catalog_type", cancellationToken);

        dbContext.CatalogBrands.AddRange(snapshot.Brands.Select(item =>
            new CatalogBrand { Id = item.Id, Brand = item.Brand }));
        dbContext.CatalogTypes.AddRange(snapshot.Types.Select(item =>
            new CatalogType { Id = item.Id, Type = item.Type }));
        dbContext.Catalogs.AddRange(snapshot.Items.Select(item => new Catalog
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            PictureFileName = item.PictureFileName,
            CatalogTypeId = item.CatalogTypeId,
            CatalogBrandId = item.CatalogBrandId,
            AvailableStock = item.AvailableStock,
            RestockThreshold = item.RestockThreshold,
            MaxStockThreshold = item.MaxStockThreshold,
            OnReorder = item.OnReorder
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await CatalogSeeder.AlignIdentitySequencesAsync(dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CompleteOperationAsync(
        CatalogDbContext dbContext,
        Guid operationId,
        string managedImageHashesJson,
        DateTimeOffset? initializedAtUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockCatalogAsync(dbContext, cancellationToken);
        var marker = await dbContext.CatalogInitializations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (marker is null
            || marker.Status != "resetting"
            || marker.ResetOperationId != operationId)
        {
            throw new CatalogResetException("Catalog reset state changed during the operation.");
        }

        marker.Status = "initialized";
        marker.InitializedAtUtc = initializedAtUtc ?? DateTimeOffset.UtcNow;
        marker.ManagedImageHashesJson = managedImageHashesJson;
        marker.ResetOperationId = null;
        marker.ResetStartedAtUtc = null;
        marker.ResetSnapshotJson = null;
        marker.ResetImageDirectory = null;
        marker.ResetBackupDirectory = null;
        marker.ResetImageManifestJson = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureResetPendingAsync(
        CatalogDbContext dbContext,
        Guid operationId,
        string imageDirectory,
        string? backupDirectory,
        DatabaseSnapshot snapshot,
        IReadOnlyList<ResetImageEntry> imageEntries,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockCatalogAsync(dbContext, cancellationToken);
        var marker = await dbContext.CatalogInitializations
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (marker is null
            || (marker.Status == "resetting" && marker.ResetOperationId != operationId)
            || (marker.Status != "resetting" && marker.Status != "initialized"))
        {
            throw new CatalogResetException("Catalog reset state cannot be recovered safely.");
        }

        marker.Status = "resetting";
        marker.ResetOperationId = operationId;
        marker.ResetStartedAtUtc ??= DateTimeOffset.UtcNow;
        marker.ResetSnapshotJson ??= JsonSerializer.Serialize(snapshot, JsonOptions);
        marker.ResetImageDirectory = imageDirectory;
        marker.ResetBackupDirectory = backupDirectory;
        marker.ResetImageManifestJson ??= JsonSerializer.Serialize(imageEntries, JsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task LockCatalogAsync(
        CatalogDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CatalogSeeder.InitializationLockId})",
            cancellationToken);

    private static (string? BackupDirectory, List<ResetImageEntry> Entries) PrepareImageBackup(
        string imageRoot,
        Guid operationId,
        IReadOnlyDictionary<string, byte[]> replacements,
        IReadOnlyDictionary<string, string> managedImages)
    {
        if (replacements.Count == 0)
        {
            return (null, []);
        }

        ValidateImageRoot(imageRoot);
        Directory.CreateDirectory(imageRoot);
        var existingEntries = Directory.EnumerateFileSystemEntries(imageRoot)
            .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);
        var backup = GetBackupDirectory(imageRoot, operationId);
        if (Directory.Exists(backup) || File.Exists(backup))
        {
            throw new CatalogResetException("A catalog reset backup already exists for this operation.");
        }

        Directory.CreateDirectory(Path.Combine(backup, "original"));
        Directory.CreateDirectory(Path.Combine(backup, "replacement"));
        var entries = new List<ResetImageEntry>();
        try
        {
            foreach (var (name, content) in replacements)
            {
                if (!IsSafeImageName(name) || content.Length == 0)
                {
                    throw new CatalogResetException("The selected image set contains an invalid file.");
                }

                var existingPath = existingEntries.GetValueOrDefault(name);
                var existed = existingPath is not null;
                string? originalHash = null;
                if (existed)
                {
                    if (!string.Equals(
                            Path.GetFileName(existingPath),
                            name,
                            StringComparison.Ordinal)
                        || File.GetAttributes(existingPath!).HasFlag(FileAttributes.ReparsePoint)
                        || Directory.Exists(existingPath)
                        || !managedImages.TryGetValue(name, out originalHash))
                    {
                        throw new CatalogResetException(
                            "A selected image conflicts with a file whose ownership cannot be verified.");
                    }

                    var actualHash = CatalogImageInstallationStore.GetHash(existingPath!);
                    if (!string.Equals(actualHash, originalHash, StringComparison.Ordinal))
                    {
                        throw new CatalogResetException(
                            "A selected managed image changed outside the catalog; reset was not started.");
                    }

                    File.Copy(existingPath!, Path.Combine(backup, "original", name));
                }
                else if (managedImages.ContainsKey(name))
                {
                    throw new CatalogResetException(
                        "A managed image is missing; reset was not started because its ownership state is uncertain.");
                }

                var newHash = GetHash(content);
                File.WriteAllBytes(Path.Combine(backup, "replacement", name), content);
                entries.Add(new ResetImageEntry
                {
                    Name = name,
                    Existed = existed,
                    OriginalHash = originalHash,
                    NewHash = newHash
                });
            }

            return (backup, entries);
        }
        catch
        {
            if (!TryDeleteDirectory(backup))
            {
                throw new CatalogResetException(
                    "Catalog reset preparation failed and its temporary image backup could not be removed.");
            }

            throw;
        }
    }

    private static void ApplyReplacementImages(
        string imageRoot,
        string? backupDirectory,
        IReadOnlyList<ResetImageEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        if (backupDirectory is null)
        {
            throw new CatalogResetException("The catalog image backup is missing.");
        }

        ValidateImageRoot(imageRoot);
        foreach (var entry in entries)
        {
            var target = Path.Combine(imageRoot, entry.Name);
            var replacement = Path.Combine(backupDirectory, "replacement", entry.Name);
            if (File.Exists(target))
            {
                if (!entry.Existed
                    || CatalogImageInstallationStore.GetHash(target) != entry.OriginalHash)
                {
                    throw new CatalogResetException(
                        "A selected image changed while catalog reset was running.");
                }
            }
            else if (entry.Existed)
            {
                throw new CatalogResetException(
                    "A selected image disappeared while catalog reset was running.");
            }

            if (!File.Exists(replacement) || CatalogImageInstallationStore.GetHash(replacement) != entry.NewHash)
            {
                throw new CatalogResetException("The staged catalog image could not be verified.");
            }

            File.Move(replacement, target, overwrite: entry.Existed);
        }
    }

    private static void RestoreImages(
        string imageRoot,
        string? backupDirectory,
        IReadOnlyList<ResetImageEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        ValidateImageRoot(imageRoot);
        foreach (var entry in entries)
        {
            var target = Path.Combine(imageRoot, entry.Name);
            var exists = File.Exists(target);
            if (exists && File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new CatalogResetException("A catalog image path changed to an unsupported link.");
            }

            if (entry.Existed)
            {
                var original = backupDirectory is null
                    ? null
                    : Path.Combine(backupDirectory, "original", entry.Name);
                if (original is null
                    || !File.Exists(original)
                    || CatalogImageInstallationStore.GetHash(original) != entry.OriginalHash)
                {
                    throw new CatalogResetException("A catalog image backup is missing or invalid.");
                }

                if (exists)
                {
                    var currentHash = CatalogImageInstallationStore.GetHash(target);
                    if (currentHash != entry.OriginalHash && currentHash != entry.NewHash)
                    {
                        throw new CatalogResetException(
                            "A catalog image changed during recovery; it was preserved for manual review.");
                    }
                }
                else
                {
                    Directory.CreateDirectory(imageRoot);
                }

                File.Copy(original, target, overwrite: true);
            }
            else if (exists)
            {
                if (CatalogImageInstallationStore.GetHash(target) != entry.NewHash)
                {
                    throw new CatalogResetException(
                        "An unrecognized image occupies a reset target; it was preserved for manual review.");
                }

                File.Delete(target);
            }
        }
    }

    private static void ValidateImageRoot(string imageRoot)
    {
        try
        {
            CatalogImageInstallationStore.ValidateImageDirectory(imageRoot);
        }
        catch (CatalogCustomizationException exception)
        {
            throw new CatalogResetException(exception.Message);
        }
    }

    private static string GetBackupDirectory(string imageRoot, Guid operationId)
    {
        var parent = Directory.GetParent(imageRoot)?.FullName
            ?? throw new CatalogResetException("The catalog image directory is invalid.");
        var name = Path.GetFileName(imageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(parent, $".{name}.catalog-reset-{operationId:N}");
    }

    private static Dictionary<string, string> DeserializeManagedImages(string json)
    {
        try
        {
            var images = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? throw new JsonException();
            return new Dictionary<string, string>(images, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            throw new CatalogResetException("Catalog image ownership metadata is invalid; reset was not started.");
        }
    }

    private static DatabaseSnapshot DeserializeSnapshot(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DatabaseSnapshot>(json, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new CatalogResetException("The saved catalog recovery snapshot is invalid.");
        }
    }

    private static List<ResetImageEntry> DeserializeImageEntries(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ResetImageEntry>>(json, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new CatalogResetException("The saved catalog image recovery manifest is invalid.");
        }
    }

    private static bool TryDeleteDirectory(string? path)
    {
        if (path is null || !Directory.Exists(path))
        {
            return true;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetHash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));

    private static bool IsSafeImageName(string name) =>
        name.Length > 0
        && name is not "." and not ".."
        && string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
        && name.IndexOfAny(['/', '\\', ':', '\0']) < 0;

    private static void ValidateConfirmation(string confirmation, string expected)
    {
        if (!string.Equals(confirmation, expected, StringComparison.Ordinal))
        {
            throw new CatalogResetException("The required catalog confirmation is missing or does not match.");
        }
    }

    private sealed class DatabaseSnapshot
    {
        public DatabaseSnapshot() { }
        public DateTimeOffset InitializedAtUtc { get; set; }
        public string ManagedImageHashesJson { get; set; } = "{}";
        public List<BrandSnapshot> Brands { get; set; } = [];
        public List<TypeSnapshot> Types { get; set; } = [];
        public List<ItemSnapshot> Items { get; set; } = [];
    }

    private sealed class BrandSnapshot
    {
        public BrandSnapshot() { }
        public int Id { get; set; }
        public string Brand { get; set; } = string.Empty;
    }

    private sealed class TypeSnapshot
    {
        public TypeSnapshot() { }
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    private sealed class ItemSnapshot
    {
        public ItemSnapshot() { }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string PictureFileName { get; set; } = string.Empty;
        public int CatalogTypeId { get; set; }
        public int CatalogBrandId { get; set; }
        public int AvailableStock { get; set; }
        public int RestockThreshold { get; set; }
        public int MaxStockThreshold { get; set; }
        public bool OnReorder { get; set; }
    }

    private sealed class ResetImageEntry
    {
        public ResetImageEntry() { }
        public string Name { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public string? OriginalHash { get; set; }
        public string NewHash { get; set; } = string.Empty;
    }
}
