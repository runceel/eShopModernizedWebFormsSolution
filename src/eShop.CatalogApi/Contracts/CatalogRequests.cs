using System.ComponentModel.DataAnnotations;

namespace eShop.CatalogApi.Contracts;

/// <summary>Fields accepted when creating a catalog item.</summary>
public sealed record CreateCatalogItemRequest : IValidatableObject
{
    /// <summary>The required catalog item name, up to 50 characters.</summary>
    [Required]
    [StringLength(50)]
    public string? Name { get; init; }

    /// <summary>An optional item description.</summary>
    public string? Description { get; init; }

    /// <summary>The price from 0 to 1,000,000 with no more than two decimals.</summary>
    [Required]
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; init; }

    /// <summary>The ID of an existing catalog brand.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? CatalogBrandId { get; init; }

    /// <summary>The ID of an existing catalog type.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? CatalogTypeId { get; init; }

    /// <summary>The available stock from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? AvailableStock { get; init; }

    /// <summary>The restock threshold from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? RestockThreshold { get; init; }

    /// <summary>The maximum stock threshold from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? MaxStockThreshold { get; init; }

    /// <summary>Validates price precision beyond the range attribute.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price is decimal price && CatalogRequestValidation.HasMoreThanTwoDecimalPlaces(price))
        {
            yield return new ValidationResult(
                "Price must have no more than two decimal places.",
                [nameof(Price)]);
        }
    }
}

/// <summary>Editable fields accepted when updating a catalog item.</summary>
public sealed record UpdateCatalogItemRequest : IValidatableObject
{
    /// <summary>The required catalog item name, up to 50 characters.</summary>
    [Required]
    [StringLength(50)]
    public string? Name { get; init; }

    /// <summary>An optional item description.</summary>
    public string? Description { get; init; }

    /// <summary>The price from 0 to 1,000,000 with no more than two decimals.</summary>
    [Required]
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; init; }

    /// <summary>The ID of an existing catalog brand.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? CatalogBrandId { get; init; }

    /// <summary>The ID of an existing catalog type.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? CatalogTypeId { get; init; }

    /// <summary>The available stock from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? AvailableStock { get; init; }

    /// <summary>The restock threshold from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? RestockThreshold { get; init; }

    /// <summary>The maximum stock threshold from 0 to 10,000,000.</summary>
    [Required]
    [Range(0, 10000000)]
    public int? MaxStockThreshold { get; init; }

    /// <summary>Validates price precision beyond the range attribute.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price is decimal price && CatalogRequestValidation.HasMoreThanTwoDecimalPlaces(price))
        {
            yield return new ValidationResult(
                "Price must have no more than two decimal places.",
                [nameof(Price)]);
        }
    }

}

internal static class CatalogRequestValidation
{
    internal static bool HasMoreThanTwoDecimalPlaces(decimal value) =>
        ((decimal.GetBits(value)[3] >> 16) & 0xff) > 2;
}
