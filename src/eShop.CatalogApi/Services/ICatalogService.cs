using eShop.CatalogApi.Contracts;

namespace eShop.CatalogApi.Services;

internal interface ICatalogService
{
    Task<CatalogItemPageResponse> GetPageAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CatalogItemResult?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<CatalogItemResult> CreateAsync(
        CreateCatalogItemRequest request,
        CancellationToken cancellationToken);

    Task<CatalogItemResult?> UpdateAsync(
        int id,
        UpdateCatalogItemRequest request,
        uint expectedVersion,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}

internal sealed record CatalogItemResult(CatalogItemResponse Item, uint Version);
