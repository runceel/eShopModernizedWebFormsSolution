using Microsoft.AspNetCore.Http;

namespace eShop.Web.Services;

internal static class LegacyHttpCompatibilityExtensions
{
    internal static void ApplyLegacyNotFoundStatus(this HttpResponse response) =>
        response.StatusCode = StatusCodes.Status500InternalServerError;
}
