using eShop.CatalogApi.Contracts;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;

namespace eShop.CatalogApi.Services;

internal sealed class CatalogService(CatalogDbContext dbContext) : ICatalogService
{
    private const int MaximumPageSize = 100;
    private const int MaximumSkipCount = int.MaxValue;

    public async Task<CatalogItemPageResponse> GetPageAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (pageIndex < 0 || (long)pageIndex * pageSize > MaximumSkipCount)
        {
            errors[nameof(pageIndex)] = ["Page index is outside the supported range."];
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            errors[nameof(pageSize)] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new CatalogValidationException(errors);
        }

        var totalItems = await dbContext.Catalogs.LongCountAsync(cancellationToken);
        var items = await dbContext.Catalogs
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Skip((int)((long)pageIndex * pageSize))
            .Take(pageSize)
            .Select(item => new CatalogItemResponse(
                item.Id,
                item.Name,
                item.Description,
                item.Price,
                item.PictureFileName,
                item.CatalogBrandId,
                item.CatalogBrand.Brand,
                item.CatalogTypeId,
                item.CatalogType.Type,
                item.AvailableStock,
                item.RestockThreshold,
                item.MaxStockThreshold,
                item.OnReorder))
            .ToListAsync(cancellationToken);

        return new CatalogItemPageResponse(
            items,
            pageIndex,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (totalItems + pageSize - 1) / pageSize);
    }

    public async Task<IReadOnlyList<CatalogBrandResponse>> GetBrandsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CatalogBrands
            .AsNoTracking()
            .OrderBy(brand => brand.Id)
            .Select(brand => new CatalogBrandResponse(brand.Id, brand.Brand))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogTypeResponse>> GetTypesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CatalogTypes
            .AsNoTracking()
            .OrderBy(type => type.Id)
            .Select(type => new CatalogTypeResponse(type.Id, type.Type))
            .ToListAsync(cancellationToken);

    public async Task<CatalogItemResult?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Catalogs
            .AsNoTracking()
            .Include(catalog => catalog.CatalogBrand)
            .Include(catalog => catalog.CatalogType)
            .SingleOrDefaultAsync(catalog => catalog.Id == id, cancellationToken);

        return item is null ? null : ToResult(item);
    }

    public async Task<CatalogItemResult> CreateAsync(
        CreateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureReferencesExistAsync(
            request.CatalogBrandId!.Value,
            request.CatalogTypeId!.Value,
            cancellationToken);

        var item = new Catalog
        {
            Name = request.Name!,
            Description = request.Description,
            Price = request.Price!.Value,
            PictureFileName = "dummy.png",
            CatalogBrandId = request.CatalogBrandId.Value,
            CatalogTypeId = request.CatalogTypeId.Value,
            AvailableStock = request.AvailableStock!.Value,
            RestockThreshold = request.RestockThreshold!.Value,
            MaxStockThreshold = request.MaxStockThreshold!.Value,
            OnReorder = false
        };

        dbContext.Catalogs.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(item).Reference(catalog => catalog.CatalogBrand).LoadAsync(cancellationToken);
        await dbContext.Entry(item).Reference(catalog => catalog.CatalogType).LoadAsync(cancellationToken);
        return ToResult(item);
    }

    public async Task<CatalogItemResult?> UpdateAsync(
        int id,
        UpdateCatalogItemRequest request,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Catalogs
            .Include(catalog => catalog.CatalogBrand)
            .Include(catalog => catalog.CatalogType)
            .SingleOrDefaultAsync(catalog => catalog.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.Version != expectedVersion)
        {
            throw new CatalogConcurrencyException(id);
        }

        await EnsureReferencesExistAsync(
            request.CatalogBrandId!.Value,
            request.CatalogTypeId!.Value,
            cancellationToken);

        item.Name = request.Name!;
        item.Description = request.Description;
        item.Price = request.Price!.Value;
        item.CatalogBrandId = request.CatalogBrandId.Value;
        item.CatalogTypeId = request.CatalogTypeId.Value;
        item.CatalogBrand = await dbContext.CatalogBrands
            .SingleAsync(brand => brand.Id == request.CatalogBrandId.Value, cancellationToken);
        item.CatalogType = await dbContext.CatalogTypes
            .SingleAsync(type => type.Id == request.CatalogTypeId.Value, cancellationToken);
        item.AvailableStock = request.AvailableStock!.Value;
        item.RestockThreshold = request.RestockThreshold!.Value;
        item.MaxStockThreshold = request.MaxStockThreshold!.Value;

        try
        {
            dbContext.Entry(item).Property(catalog => catalog.Name).IsModified = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CatalogConcurrencyException(id, exception);
        }

        return ToResult(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var item = await dbContext.Catalogs
            .SingleOrDefaultAsync(catalog => catalog.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        dbContext.Catalogs.Remove(item);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CatalogConcurrencyException(id, exception);
        }

        return true;
    }

    private async Task EnsureReferencesExistAsync(
        int catalogBrandId,
        int catalogTypeId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await dbContext.CatalogBrands.AnyAsync(
                brand => brand.Id == catalogBrandId,
                cancellationToken))
        {
            errors[nameof(CreateCatalogItemRequest.CatalogBrandId)] =
                ["The selected catalog brand does not exist."];
        }

        if (!await dbContext.CatalogTypes.AnyAsync(
                type => type.Id == catalogTypeId,
                cancellationToken))
        {
            errors[nameof(CreateCatalogItemRequest.CatalogTypeId)] =
                ["The selected catalog type does not exist."];
        }

        if (errors.Count > 0)
        {
            throw new CatalogValidationException(errors);
        }
    }

    private static CatalogItemResult ToResult(Catalog item) =>
        new(
            new CatalogItemResponse(
                item.Id,
                item.Name,
                item.Description,
                item.Price,
                item.PictureFileName,
                item.CatalogBrandId,
                item.CatalogBrand.Brand,
                item.CatalogTypeId,
                item.CatalogType.Type,
                item.AvailableStock,
                item.RestockThreshold,
                item.MaxStockThreshold,
                item.OnReorder),
            item.Version);
}
