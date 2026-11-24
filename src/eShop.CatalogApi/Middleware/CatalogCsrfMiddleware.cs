using Microsoft.AspNetCore.Http.HttpResults;

namespace eShop.CatalogApi.Middleware;

internal static class CatalogCsrfMiddleware
{
    internal static IApplicationBuilder UseCatalogCsrfProtection(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsStateChangingRequest(context.Request)
                && context.Request.Path.StartsWithSegments("/api/catalog")
                && !IsSameOrigin(context.Request))
            {
                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Cross-origin request rejected",
                    Detail = "Catalog changes must originate from the same origin.",
                    Instance = context.Request.Path
                };
                problem.Extensions["code"] = "csrf_origin_rejected";
                await TypedResults.Problem(problem).ExecuteAsync(context);
                return;
            }

            await next(context);
        });

    private static bool IsStateChangingRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        || HttpMethods.IsPut(request.Method)
        || HttpMethods.IsPatch(request.Method)
        || HttpMethods.IsDelete(request.Method);

    private static bool IsSameOrigin(HttpRequest request)
    {
        var origins = request.Headers.Origin;
        if (!Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(origins))
        {
            if (origins.Count != 1
                || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin)
                || origin.AbsolutePath != "/"
                || origin.Query.Length > 0
                || origin.Fragment.Length > 0
                || !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var requestOrigin))
            {
                return false;
            }

            return Uri.Compare(
                origin,
                requestOrigin,
                UriComponents.SchemeAndServer,
                UriFormat.Unescaped,
                StringComparison.OrdinalIgnoreCase) == 0;
        }

        return !string.Equals(
            request.Headers["Sec-Fetch-Site"],
            "cross-site",
            StringComparison.OrdinalIgnoreCase);
    }
}
