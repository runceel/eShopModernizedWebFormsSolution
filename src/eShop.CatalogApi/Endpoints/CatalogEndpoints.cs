using System.Globalization;
using eShop.CatalogApi.Contracts;
using eShop.CatalogApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace eShop.CatalogApi.Endpoints;

internal static class CatalogEndpoints
{
    internal static RouteGroupBuilder MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog")
            .WithTags("Catalog");

        group.MapGet("/brands", GetBrandsAsync)
            .WithName("GetCatalogBrands")
            .WithSummary("List catalog brands")
            .Produces<IReadOnlyList<CatalogBrandResponse>>();

        group.MapGet("/types", GetTypesAsync)
            .WithName("GetCatalogTypes")
            .WithSummary("List catalog types")
            .Produces<IReadOnlyList<CatalogTypeResponse>>();

        group.MapGet("", GetPageAsync)
            .WithName("GetCatalogPage")
            .WithSummary("List catalog items")
            .WithDescription("Returns catalog items ordered by ID with zero-based paging.")
            .Produces<CatalogItemPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:int}", GetByIdAsync)
            .WithName("GetCatalogItem")
            .WithSummary("Get a catalog item")
            .WithDescription("Returns a catalog item and its current strong ETag.")
            .Produces<CatalogItemResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", CreateAsync)
            .WithName("CreateCatalogItem")
            .WithSummary("Create a catalog item")
            .WithDescription("Creates an item with the default picture and reorder state.")
            .Produces<CatalogItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:int}", UpdateAsync)
            .WithName("UpdateCatalogItem")
            .WithSummary("Update a catalog item")
            .WithDescription("Requires the current strong ETag in the If-Match header.")
            .Produces<CatalogItemResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{id:int}", DeleteAsync)
            .WithName("DeleteCatalogItem")
            .WithSummary("Delete a catalog item")
            .WithDescription("Deletes an item by ID.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<CatalogBrandResponse>>> GetBrandsAsync(
        ICatalogService catalogService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalogService.GetBrandsAsync(cancellationToken));

    private static async Task<Ok<IReadOnlyList<CatalogTypeResponse>>> GetTypesAsync(
        ICatalogService catalogService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalogService.GetTypesAsync(cancellationToken));

    private static async Task<Ok<CatalogItemPageResponse>> GetPageAsync(
        ICatalogService catalogService,
        CancellationToken cancellationToken,
        int? pageIndex = null,
        int? pageSize = null) =>
        TypedResults.Ok(await catalogService.GetPageAsync(
            pageIndex ?? 0,
            pageSize ?? 10,
            cancellationToken));

    private static async Task<Results<Ok<CatalogItemResponse>, ProblemHttpResult>> GetByIdAsync(
        int id,
        HttpContext httpContext,
        ICatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFoundProblem(id, httpContext.Request.Path);
        }

        httpContext.Response.Headers.ETag = FormatETag(result.Version);
        return TypedResults.Ok(result.Item);
    }

    private static async Task<Created<CatalogItemResponse>> CreateAsync(
        CreateCatalogItemRequest request,
        HttpContext httpContext,
        ICatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateAsync(request, cancellationToken);
        httpContext.Response.Headers.ETag = FormatETag(result.Version);
        return TypedResults.Created($"/api/catalog/{result.Item.Id}", result.Item);
    }

    private static async Task<Results<Ok<CatalogItemResponse>, ProblemHttpResult>> UpdateAsync(
        int id,
        UpdateCatalogItemRequest request,
        HttpRequest httpRequest,
        HttpContext httpContext,
        ICatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var expectedVersion = ParseIfMatch(httpRequest.Headers.IfMatch);
        var result = await catalogService.UpdateAsync(id, request, expectedVersion, cancellationToken);
        if (result is null)
        {
            return NotFoundProblem(id, httpContext.Request.Path);
        }

        httpContext.Response.Headers.ETag = FormatETag(result.Version);
        return TypedResults.Ok(result.Item);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        int id,
        HttpContext httpContext,
        ICatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var deleted = await catalogService.DeleteAsync(id, cancellationToken);
        return deleted
            ? TypedResults.NoContent()
            : NotFoundProblem(id, httpContext.Request.Path);
    }

    private static ProblemHttpResult NotFoundProblem(int id, string path)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Catalog item not found",
            Detail = "No catalog item exists with the requested ID.",
            Instance = path
        };
        problem.Extensions["code"] = "catalog_item_not_found";
        problem.Extensions["resourceId"] = id;
        return TypedResults.Problem(problem);
    }

    private static string FormatETag(uint version) =>
        $"\"{version.ToString("x8", CultureInfo.InvariantCulture)}\"";

    private static uint ParseIfMatch(Microsoft.Extensions.Primitives.StringValues values)
    {
        if (Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(values))
        {
            throw new CatalogPreconditionRequiredException();
        }

        if (values.Count != 1
            || !EntityTagHeaderValue.TryParse(values[0], out var entityTag)
            || entityTag is null
            || entityTag.IsWeak
            || entityTag.Tag == "*")
        {
            throw new CatalogInvalidPreconditionException();
        }

        var tag = entityTag.Tag.Value;
        if (tag is null
            || tag.Length != 10
            || tag[0] != '"'
            || tag[^1] != '"'
            || !uint.TryParse(
                tag.AsSpan(1, 8),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var version))
        {
            throw new CatalogInvalidPreconditionException();
        }

        return version;
    }
}
