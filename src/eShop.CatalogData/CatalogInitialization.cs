namespace eShop.CatalogData;

public sealed class CatalogInitialization
{
    public int Id { get; set; }
    public DateTimeOffset InitializedAtUtc { get; set; }
    public string Status { get; set; } = "initialized";
    public string ManagedImageHashesJson { get; set; } = "{}";
    public Guid? ResetOperationId { get; set; }
    public DateTimeOffset? ResetStartedAtUtc { get; set; }
    public string? ResetSnapshotJson { get; set; }
    public string? ResetImageDirectory { get; set; }
    public string? ResetBackupDirectory { get; set; }
    public string? ResetImageManifestJson { get; set; }
}
