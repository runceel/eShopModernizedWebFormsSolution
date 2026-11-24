using System.Text.Json;
using eShop.CatalogApi.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace eShop.CatalogApi.Middleware;

internal sealed class CatalogApiExceptionHandler(
    ILogger<CatalogApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, code, errors) = exception switch
        {
            CatalogValidationException validation =>
                (StatusCodes.Status400BadRequest, "Validation failed",
                    "One or more request fields are invalid.", "validation_error",
                    validation.Errors),
            CatalogConcurrencyException concurrencyException =>
                (StatusCodes.Status409Conflict, "Catalog item conflict",
                    "The catalog item changed after it was read. Reload it before editing.",
                    "concurrency_conflict", (IDictionary<string, string[]>?)null),
            CatalogPreconditionRequiredException =>
                (StatusCodes.Status428PreconditionRequired, "Precondition required",
                    "Send the current ETag in the If-Match header.",
                    "precondition_required", (IDictionary<string, string[]>?)null),
            CatalogInvalidPreconditionException =>
                (StatusCodes.Status400BadRequest, "Invalid precondition",
                    "If-Match must contain one strong catalog ETag.",
                    "invalid_precondition", (IDictionary<string, string[]>?)null),
            BadHttpRequestException { InnerException: JsonException jsonException } =>
                (StatusCodes.Status400BadRequest, "Validation failed",
                    "One or more request fields are invalid.", "validation_error",
                    GetJsonBindingErrors(jsonException)),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Invalid request",
                    "The request could not be parsed.", "invalid_request",
                    (IDictionary<string, string[]>?)null),
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation
                }
            } =>
                (StatusCodes.Status409Conflict, "Catalog data conflict",
                    "The catalog item could not be saved because its references changed.",
                    "data_conflict", (IDictionary<string, string[]>?)null),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error",
                "The request could not be completed.", "internal_error",
                (IDictionary<string, string[]>?)null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing catalog request.");
        }
        else
        {
            logger.LogInformation(exception, "Catalog request rejected with code {ErrorCode}.", code);
        }

        ProblemDetails problem = errors is null
            ? new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
            : new ValidationProblemDetails(errors)
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
        problem.Extensions["code"] = code;
        if (exception is CatalogConcurrencyException conflict)
        {
            problem.Extensions["resourceId"] = conflict.CatalogId;
        }

        httpContext.Response.StatusCode = statusCode;
        await TypedResults.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }

    private static IDictionary<string, string[]> GetJsonBindingErrors(JsonException exception)
    {
        var path = exception.Path;
        var field = path is null or "$"
            ? "request"
            : path[(path.LastIndexOf('.') + 1)..];
        var bracketIndex = field.IndexOf('[');
        if (bracketIndex >= 0)
        {
            field = field[..bracketIndex];
        }

        return new Dictionary<string, string[]>
        {
            [field] = ["The supplied value has an invalid format."]
        };
    }
}
