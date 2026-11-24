using eShop.Web.Services;
using Xunit;

namespace eShop.Web.Tests;

public sealed class LegacyCatalogRouteTests
{
    [Theory]
    [InlineData("/Default/index/1/size/10", 1, 10)]
    [InlineData("default/INDEX/2/SIZE/5?source=legacy", 2, 5)]
    public void TryParsePagedListReadsLegacyPagingSegments(
        string path,
        int expectedPageIndex,
        int expectedPageSize)
    {
        var parsed = LegacyCatalogRoute.TryParsePagedList(path, out var pageIndex, out var pageSize);

        Assert.True(parsed);
        Assert.Equal(expectedPageIndex, pageIndex);
        Assert.Equal(expectedPageSize, pageSize);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Default")]
    [InlineData("/Default/index/1")]
    [InlineData("/Default/index/one/size/10")]
    public void TryParsePagedListRejectsOtherRoutes(string path)
    {
        var parsed = LegacyCatalogRoute.TryParsePagedList(path, out var pageIndex, out var pageSize);

        Assert.False(parsed);
        Assert.Equal(0, pageIndex);
        Assert.Equal(0, pageSize);
    }
}
