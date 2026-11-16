var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var catalogDb = postgres.AddDatabase("catalog");

var catalogInit = builder.AddProject("catalog-init", "../eShop.CatalogInit/eShop.CatalogInit.csproj")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);

var catalogApi = builder.AddProject("catalog-api", "../eShop.CatalogApi/eShop.CatalogApi.csproj")
    .WithReference(catalogDb)
    .WithHttpHealthCheck("/health")
    .WaitForCompletion(catalogInit);

builder.AddProject("web", "../eShop.Web/eShop.Web.csproj")
    .WithReference(catalogApi)
    .WithHttpHealthCheck("/health")
    .WaitFor(catalogApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
