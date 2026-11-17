namespace eShop.CatalogApi.Services;

internal sealed class CatalogValidationException(
    IDictionary<string, string[]> errors) : Exception("Catalog request validation failed.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

internal sealed class CatalogConcurrencyException(
    int catalogId,
    Exception? innerException = null)
    : Exception($"Catalog item {catalogId} was modified by another request.", innerException)
{
    public int CatalogId { get; } = catalogId;
}

internal sealed class CatalogPreconditionRequiredException()
    : Exception("An If-Match header is required.");

internal sealed class CatalogInvalidPreconditionException()
    : Exception("The If-Match header must contain one strong catalog ETag.");
