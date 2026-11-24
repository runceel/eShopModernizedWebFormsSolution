namespace eShop.CatalogApi.Contracts;

/// <summary>A catalog item and its associated brand and type.</summary>
public sealed record CatalogItemResponse(
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

/// <summary>A page of catalog items.</summary>
public sealed record CatalogItemPageResponse(
    IReadOnlyList<CatalogItemResponse> Items,
    int PageIndex,
    int PageSize,
    long TotalItems,
    long TotalPages);

/// <summary>A catalog brand available to item forms.</summary>
public sealed record CatalogBrandResponse(int Id, string Brand);

/// <summary>A catalog type available to item forms.</summary>
public sealed record CatalogTypeResponse(int Id, string Type);
