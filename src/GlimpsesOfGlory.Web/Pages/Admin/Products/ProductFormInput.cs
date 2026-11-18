using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public sealed class ProductFormInput : IValidatableObject
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, 100000, ErrorMessage = "Stock quantity can't be negative.")]
    public int StockQuantity { get; set; }

    public bool IsPreorder { get; set; }

    public DateOnly? ExpectedAvailabilityDate { get; set; }

    // Off: the product ships at the store default rate (/admin/shipping).
    public bool HasCustomShipping { get; set; }

    [Range(0, 100000, ErrorMessage = "Shipping can't be negative.")]
    public decimal? ShippingFirstItemAmount { get; set; }

    [Range(0, 100000, ErrorMessage = "Shipping can't be negative.")]
    public decimal? ShippingAdditionalItemAmount { get; set; }

    public static ProductFormInput From(AdminProductDetail product) => new()
    {
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        StockQuantity = product.StockQuantity,
        IsPreorder = product.IsPreorder,
        ExpectedAvailabilityDate = product.ExpectedAvailabilityDate,
        HasCustomShipping = product.ShippingRate is not null,
        ShippingFirstItemAmount = product.ShippingRate?.FirstItemAmount,
        ShippingAdditionalItemAmount = product.ShippingRate?.AdditionalItemAmount,
    };

    public ProductEditRequest ToRequest() => new(
        Name,
        Description,
        Price,
        StockQuantity,
        IsPreorder,
        ExpectedAvailabilityDate,
        HasCustomShipping ? new ProductShippingRate(ShippingFirstItemAmount!.Value, ShippingAdditionalItemAmount!.Value) : null);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!HasCustomShipping)
        {
            yield break;
        }

        if (ShippingFirstItemAmount is null)
        {
            yield return new ValidationResult("Enter the first-item shipping rate.", [nameof(ShippingFirstItemAmount)]);
        }

        if (ShippingAdditionalItemAmount is null)
        {
            yield return new ValidationResult("Enter the additional-item shipping rate.", [nameof(ShippingAdditionalItemAmount)]);
        }
    }
}
