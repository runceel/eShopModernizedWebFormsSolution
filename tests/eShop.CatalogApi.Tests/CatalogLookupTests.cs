using System.Net.Http.Json;
using eShop.CatalogApi.Contracts;
using Xunit;

namespace eShop.CatalogApi.Tests;

[Collection("PostgreSQL")]
public sealed class CatalogLookupTests(PostgreSqlContainer container) : CatalogTestBase(container)
{
    [Fact]
    public async Task GetBrandsReturnsAllSeededOptionsInIdOrder()
    {
        var brands = await Client.GetFromJsonAsync<CatalogBrandResponse[]>("/api/catalog/brands");

        Assert.NotNull(brands);
        Assert.Equal(
            ["Azure", ".NET", "Visual Studio", "SQL Server", "Other"],
            brands.Select(brand => brand.Brand));
    }

    [Fact]
    public async Task GetTypesReturnsAllSeededOptionsInIdOrder()
    {
        var types = await Client.GetFromJsonAsync<CatalogTypeResponse[]>("/api/catalog/types");

        Assert.NotNull(types);
        Assert.Equal(
            ["Mug", "T-Shirt", "Sheet", "USB Memory Stick"],
            types.Select(type => type.Type));
    }
}
