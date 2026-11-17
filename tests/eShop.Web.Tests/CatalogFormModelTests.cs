using System.ComponentModel.DataAnnotations;
using eShop.Web.Services;
using Xunit;

namespace eShop.Web.Tests;

public sealed class CatalogFormModelTests
{
    [Fact]
    public void ValidateAcceptsAValidCatalogForm()
    {
        var errors = Validate(ValidModel());

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRejectsMissingName()
    {
        var model = ValidModel();
        model.Name = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Name)));
    }

    [Fact]
    public void ValidateRejectsPriceWithMoreThanTwoDecimalPlaces()
    {
        var model = ValidModel();
        model.Price = 19.501m;

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Price)));
    }

    private static CatalogFormModel ValidModel() =>
        new()
        {
            Name = "New catalog item",
            Price = 19.50m,
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            AvailableStock = 10,
            RestockThreshold = 2,
            MaxStockThreshold = 20
        };

    private static IReadOnlyCollection<ValidationResult> Validate(CatalogFormModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
