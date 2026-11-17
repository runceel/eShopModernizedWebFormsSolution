namespace eShop.Web.Contracts;

public sealed record CatalogItemDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    string PictureFileName,
    int CatalogBrandId,
    string CatalogBrand,
    int CatalogTypeId,
    string CatalogType,
    int AvailableStock,
    int RestockThreshold,
    int MaxStockThreshold,
    bool OnReorder);

public sealed record CatalogItemPageDto(
    IReadOnlyList<CatalogItemDto> Items,
    int PageIndex,
    int PageSize,
    long TotalItems,
    long TotalPages);

public sealed record CatalogBrandDto(int Id, string Brand);

public sealed record CatalogTypeDto(int Id, string Type);

public sealed record CatalogItemRequest(
    string? Name,
    string? Description,
    decimal? Price,
    int? CatalogBrandId,
    int? CatalogTypeId,
    int? AvailableStock,
    int? RestockThreshold,
    int? MaxStockThreshold);

public sealed record CatalogItemWithETag(CatalogItemDto Item, string ETag);

public sealed class CatalogApiProblemDto
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? Code { get; init; }

    public int? ResourceId { get; init; }

    public Dictionary<string, string[]>? Errors { get; init; }
}
