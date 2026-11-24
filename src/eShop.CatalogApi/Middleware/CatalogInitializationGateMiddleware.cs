using System.Data.Common;
using System.Text.Json;
using eShop.CatalogData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eShop.CatalogApi.Middleware;

internal sealed class CatalogInitializationGateMiddleware(
    RequestDelegate next,
    ILogger<CatalogInitializationGateMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, CatalogDbContext dbContext)
    {
        if (!context.Request.Path.StartsWithSegments("/api/catalog"))
        {
            await next(context);
            return;
        }

        try
        {
            var statuses = await dbContext.CatalogInitializations
                .AsNoTracking()
                .OrderBy(marker => marker.Id)
                .Take(2)
                .Select(marker => new { marker.Status, marker.ResetOperationId })
                .ToListAsync(context.RequestAborted);

            if (statuses is [{ Status: "initialized", ResetOperationId: null }])
            {
                await next(context);
                return;
            }
        }
        catch (DbException)
        {
            logger.LogWarning("Catalog operation blocked because database readiness could not be verified.");
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = "Catalog is temporarily unavailable.",
            Status = StatusCodes.Status503ServiceUnavailable
        };
        problem.Extensions["code"] = "catalog_unavailable";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }
}

internal static class CatalogInitializationGateExtensions
{
    public static IApplicationBuilder UseCatalogInitializationGate(this IApplicationBuilder app) =>
        app.UseMiddleware<CatalogInitializationGateMiddleware>();
}
