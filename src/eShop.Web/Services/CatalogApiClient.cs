using System.Net;
using System.Net.Http.Json;
using eShop.Web.Contracts;

namespace eShop.Web.Services;

public sealed class CatalogApiClient(HttpClient httpClient)
{
    private const int ApiPageSize = 100;

    public async Task<IReadOnlyList<CatalogItemDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var firstPage = await GetPageAsync(0, cancellationToken);
        var items = new List<CatalogItemDto>(firstPage.Items);

        for (var pageIndex = 1; pageIndex < firstPage.TotalPages; pageIndex++)
        {
            var page = await GetPageAsync(pageIndex, cancellationToken);
            if (page.PageIndex != pageIndex)
            {
                throw new InvalidDataException("Catalog API returned an unexpected page index.");
            }

            items.AddRange(page.Items);
        }

        return items;
    }

    public Task<IReadOnlyList<CatalogBrandDto>> GetBrandsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CatalogBrandDto>("api/catalog/brands", cancellationToken);

    public Task<IReadOnlyList<CatalogTypeDto>> GetTypesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CatalogTypeDto>("api/catalog/types", cancellationToken);

    public async Task<CatalogItemWithETag> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/catalog/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var item = await ReadRequiredAsync<CatalogItemDto>(response, cancellationToken);
        var etag = response.Headers.ETag?.ToString()
            ?? throw new InvalidDataException("Catalog API omitted the item ETag.");

        return new CatalogItemWithETag(item, etag);
    }

    public async Task<CatalogItemDto> CreateAsync(
        CatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/catalog",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<CatalogItemDto>(response, cancellationToken);
    }

    public async Task<CatalogItemDto> UpdateAsync(
        int id,
        CatalogItemRequest request,
        string etag,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"api/catalog/{id}")
        {
            Content = JsonContent.Create(request)
        };
        if (!httpRequest.Headers.TryAddWithoutValidation("If-Match", etag))
        {
            throw new InvalidOperationException("Could not set the Catalog API If-Match header.");
        }

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<CatalogItemDto>(response, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/catalog/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<CatalogItemPageDto> GetPageAsync(
        int pageIndex,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/catalog?pageIndex={pageIndex}&pageSize={ApiPageSize}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<CatalogItemPageDto>(response, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<List<T>>(response, cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException($"Catalog API returned an empty {typeof(T).Name} response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await response.Content.ReadFromJsonAsync<CatalogApiProblemDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Catalog API returned an empty problem response.");

        throw new CatalogApiClientException(response.StatusCode, problem);
    }
}

public sealed class CatalogApiClientException(
    HttpStatusCode statusCode,
    CatalogApiProblemDto problem)
    : Exception(problem.Detail ?? problem.Title ?? $"Catalog API returned {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Code { get; } = problem.Code;

    public bool IsConcurrencyConflict =>
        StatusCode == HttpStatusCode.Conflict && Code == "concurrency_conflict";

    public int? ResourceId { get; } = problem.ResourceId;

    public IReadOnlyDictionary<string, string[]> Errors { get; } =
        problem.Errors ?? new Dictionary<string, string[]>();

    public string? Title { get; } = problem.Title;
}
