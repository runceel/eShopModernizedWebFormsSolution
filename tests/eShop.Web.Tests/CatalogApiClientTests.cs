using System.Net;
using System.Net.Http.Json;
using eShop.Web.Contracts;
using eShop.Web.Services;
using Xunit;

namespace eShop.Web.Tests;

public sealed class CatalogApiClientTests
{
    [Fact]
    public async Task GetAllAsyncLoadsEveryApiPage()
    {
        var requestedPageIndexes = new List<int>();
        using var httpClient = CreateClient(request =>
        {
            var pageIndex = int.Parse(
                request.RequestUri!.Query.TrimStart('?')
                    .Split('&')
                    .Single(value => value.StartsWith("pageIndex=", StringComparison.Ordinal))
                    .AsSpan("pageIndex=".Length));
            requestedPageIndexes.Add(pageIndex);

            var firstId = pageIndex == 0 ? 1 : 101;
            var count = pageIndex == 0 ? 100 : 1;
            var items = Enumerable.Range(firstId, count).Select(CreateItem).ToArray();
            return JsonResponse(new CatalogItemPageDto(items, pageIndex, 100, 101, 2));
        });
        var api = new CatalogApiClient(httpClient);

        var items = await api.GetAllAsync();

        Assert.Equal(101, items.Count);
        Assert.Equal([0, 1], requestedPageIndexes);
        Assert.Equal(101, items[^1].Id);
    }

    [Fact]
    public async Task UpdateAsyncPassesEtagAndSurfacesConcurrencyProblem()
    {
        string? ifMatch = null;
        using var httpClient = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.True(request.Headers.TryGetValues("If-Match", out var values));
            ifMatch = values!.Single();
            return JsonResponse(new CatalogApiProblemDto
            {
                Status = 409,
                Title = "Catalog item conflict",
                Detail = "Catalog item 7 changed after it was read.",
                Code = "concurrency_conflict",
                ResourceId = 7
            }, HttpStatusCode.Conflict);
        });
        var api = new CatalogApiClient(httpClient);
        var request = new CatalogItemRequest("Draft", null, 10m, 1, 1, 1, 0, 10);

        var exception = await Assert.ThrowsAsync<CatalogApiClientException>(
            () => api.UpdateAsync(7, request, "\"00000001\""));

        Assert.Equal("\"00000001\"", ifMatch);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("concurrency_conflict", exception.Code);
        Assert.True(exception.IsConcurrencyConflict);
        Assert.Equal(7, exception.ResourceId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "other_error")]
    [InlineData(HttpStatusCode.BadRequest, "concurrency_conflict")]
    public void IsConcurrencyConflictRequiresMatchingStatusAndCode(HttpStatusCode status, string code)
    {
        var exception = new CatalogApiClientException(status, new CatalogApiProblemDto { Code = code });

        Assert.False(exception.IsConcurrencyConflict);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new StubHandler(handler))
        {
            BaseAddress = new Uri("http://catalog-api/")
        };

    private static HttpResponseMessage JsonResponse<T>(
        T value,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = JsonContent.Create(value)
        };

    private static CatalogItemDto CreateItem(int id) =>
        new(
            id,
            $"Item {id}",
            null,
            1.00m,
            "dummy.png",
            1,
            "Azure",
            1,
            "Mug",
            0,
            0,
            0,
            false);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
