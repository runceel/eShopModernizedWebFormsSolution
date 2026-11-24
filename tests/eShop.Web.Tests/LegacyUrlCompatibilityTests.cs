using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using eShop.Web.Contracts;
using eShop.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace eShop.Web.Tests;

public sealed class LegacyUrlCompatibilityTests(LegacyUrlTestFactory factory)
    : IClassFixture<LegacyUrlTestFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Default")]
    [InlineData("/Default.aspx")]
    public async Task ListAliasesRenderWithoutChangingTheUrl(string path)
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains("Item 1", body);
        Assert.Contains("Showing 10 of 12 products - Page 1 - 2", body);
    }

    [Fact]
    public async Task PagedListRouteRendersRequestedPageAndSizeWithoutRedirect()
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync("/Default/index/1/size/5");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/Default/index/1/size/5", response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains("Item 6", body);
        Assert.Contains("Item 10", body);
        Assert.DoesNotContain("Item 5", body);
        Assert.Contains("Showing 5 of 12 products - Page 2 - 3", body);
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Create.aspx")]
    public async Task CreateAliasesRenderWithoutRedirect(string path)
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains("<h1>Create</h1>", body);
    }

    [Theory]
    [InlineData("/Catalog/Details.aspx")]
    [InlineData("/Catalog/Edit.aspx")]
    [InlineData("/Catalog/Delete.aspx")]
    [InlineData("/Default/index/-1/size/10")]
    [InlineData("/Default/index/0/size/0")]
    public async Task LegacyErrorRoutesReturnSafeInternalServerError(string path)
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(path, response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack trace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Catalog/Details/9999")]
    [InlineData("/Catalog/Edit/9999")]
    [InlineData("/Catalog/Delete/9999")]
    public async Task MissingProductRoutesReturnSafeInternalServerError(string path)
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack trace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/About.aspx", "About.", "Your application description page.")]
    [InlineData("/Contact.aspx", "Contact.", "Your contact page.")]
    public async Task StaticContentPagesKeepTheirLegacyUrlAndBody(
        string path,
        string heading,
        string content)
    {
        using var client = factory.CreateNoRedirectClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains(heading, body);
        Assert.Contains(content, body);
        if (path == "/Contact.aspx")
        {
            Assert.Contains("One Microsoft Way", body);
            Assert.Contains("mailto:Support@example.com", body);
            Assert.Contains("mailto:Marketing@example.com", body);
        }
    }

    [Fact]
    public async Task PicsRouteServesConfiguredPhysicalImageWithoutRedirect()
    {
        var fileName = $"legacy-{Guid.NewGuid():N}.png";
        var imageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZsAAAAASUVORK5CYII=");
        var imagePath = Path.Combine(factory.ImageDirectory, fileName);
        await File.WriteAllBytesAsync(imagePath, imageBytes);

        try
        {
            using var client = factory.CreateNoRedirectClient();
            using var response = await client.GetAsync($"/Pics/{fileName}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"/Pics/{fileName}", response.RequestMessage!.RequestUri!.PathAndQuery);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(imageBytes, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            File.Delete(imagePath);
        }
    }
}

public sealed class LegacyUrlTestFactory : WebApplicationFactory<Program>
{
    private readonly string imageDirectory = Path.Combine(
        Path.GetTempPath(),
        $"eshop-web-test-images-{Guid.NewGuid():N}");

    public string ImageDirectory => imageDirectory;

    public LegacyUrlTestFactory()
    {
        Directory.CreateDirectory(imageDirectory);
    }

    public HttpClient CreateNoRedirectClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("CATALOG_API_HTTP", "http://catalog-api/");
        builder.UseSetting("CATALOG_IMAGES_PATH", imageDirectory);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CATALOG_API_HTTP"] = "http://catalog-api/",
                ["CATALOG_IMAGES_PATH"] = imageDirectory
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CatalogApiClient>();
            services.AddHttpClient<CatalogApiClient>(client =>
                    client.BaseAddress = new Uri("http://catalog-api/"))
                .ConfigurePrimaryHttpMessageHandler(() => new CatalogApiTestHandler());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(imageDirectory))
        {
            Directory.Delete(imageDirectory, recursive: true);
        }
    }

    private sealed class CatalogApiTestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            HttpResponseMessage response;

            if (path.Equals("/api/catalog/brands", StringComparison.OrdinalIgnoreCase))
            {
                response = JsonResponse(new[]
                {
                    new CatalogBrandDto(1, "Azure"),
                    new CatalogBrandDto(2, ".NET")
                });
            }
            else if (path.Equals("/api/catalog/types", StringComparison.OrdinalIgnoreCase))
            {
                response = JsonResponse(new[]
                {
                    new CatalogTypeDto(1, "Mug"),
                    new CatalogTypeDto(2, "T-Shirt")
                });
            }
            else if (path.Equals("/api/catalog", StringComparison.OrdinalIgnoreCase))
            {
                var items = Enumerable.Range(1, 12).Select(CreateItem).ToArray();
                response = JsonResponse(new CatalogItemPageDto(items, 0, 100, 12, 1));
            }
            else if (TryGetItemId(path, out var itemId) && itemId == 9999)
            {
                response = JsonResponse(new CatalogApiProblemDto
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Catalog item not found",
                    Detail = "No catalog item exists with the requested ID.",
                    Code = "catalog_item_not_found",
                    ResourceId = itemId
                }, HttpStatusCode.NotFound);
            }
            else if (TryGetItemId(path, out itemId))
            {
                response = JsonResponse(CreateItem(itemId));
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"00000001\"");
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Task.FromResult(response);
        }

        private static bool TryGetItemId(string path, out int itemId)
        {
            itemId = 0;
            var segments = path.Split('/');
            return segments.Length == 4
                && segments[1].Equals("api", StringComparison.OrdinalIgnoreCase)
                && segments[2].Equals("catalog", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(segments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
        }

        private static CatalogItemDto CreateItem(int id) =>
            new(
                id,
                $"Item {id}",
                $"Description {id}",
                10.00m + id,
                $"{id}.png",
                1,
                "Azure",
                1,
                "Mug",
                100,
                0,
                0,
                false);

        private static HttpResponseMessage JsonResponse<T>(
            T value,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            new(statusCode)
            {
                Content = JsonContent.Create(value)
            };
    }
}
