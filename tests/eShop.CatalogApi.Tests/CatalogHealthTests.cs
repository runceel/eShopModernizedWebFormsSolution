using System.Net;
using eShop.CatalogData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShop.CatalogApi.Tests;

public sealed class CatalogHealthTests(PostgreSqlContainer container) : CatalogTestBase(container)
{
    protected override bool InitializeDatabase => false;

    [Fact]
    public async Task Health_UnmigratedDatabase_IsUnhealthy()
    {
        using var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_MigratedWithoutMarker_IsUnhealthy()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        using var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_InitializerCompletesMigrationsAndMarker_IsHealthy()
    {
        await using var db = CreateDbContext();
        await CatalogSeeder.InitializeAsync(db);
        using var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
