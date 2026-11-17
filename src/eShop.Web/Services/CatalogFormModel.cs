using System.ComponentModel.DataAnnotations;
using eShop.Web.Contracts;

namespace eShop.Web.Services;

public sealed class CatalogFormModel : IValidatableObject
{
    [Required(ErrorMessage = "The Name field is required.")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "1000000",
        ErrorMessage = "The Price must be a positive number with maximum two decimals between 0 and 1 million.")]
    public decimal Price { get; set; } = 0.00m;

    [Range(1, int.MaxValue)]
    public int CatalogBrandId { get; set; }

    [Range(1, int.MaxValue)]
    public int CatalogTypeId { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
    public int AvailableStock { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Restock must be between 0 and 10 million.")]
    public int RestockThreshold { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Max stock must be between 0 and 10 million.")]
    public int MaxStockThreshold { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (((decimal.GetBits(Price)[3] >> 16) & 0xff) > 2)
        {
            yield return new ValidationResult(
                "Price must have no more than two decimal places.",
                [nameof(Price)]);
        }
    }

    public static CatalogFormModel From(CatalogItemDto item) =>
        new()
        {
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            CatalogBrandId = item.CatalogBrandId,
            CatalogTypeId = item.CatalogTypeId,
            AvailableStock = item.AvailableStock,
            RestockThreshold = item.RestockThreshold,
            MaxStockThreshold = item.MaxStockThreshold
        };

    public CatalogItemRequest ToRequest() =>
        new(
            Name,
            Description,
            Price,
            CatalogBrandId,
            CatalogTypeId,
            AvailableStock,
            RestockThreshold,
            MaxStockThreshold);
}
