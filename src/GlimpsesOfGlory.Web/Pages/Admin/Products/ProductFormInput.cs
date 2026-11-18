using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Pages.Admin.Shipping;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public sealed class ProductFormInput
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

    // Off: the product ships at the store default rate (/admin/shipping) and Shipping is ignored.
    public bool HasCustomShipping { get; set; }

    public ShippingRateInput Shipping { get; set; } = new();

    public static ProductFormInput From(AdminProductDetail product) => new()
    {
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        StockQuantity = product.StockQuantity,
        IsPreorder = product.IsPreorder,
        ExpectedAvailabilityDate = product.ExpectedAvailabilityDate,
        HasCustomShipping = product.ShippingRate is not null,
        Shipping = product.ShippingRate is { } rate ? ShippingRateInput.From(rate) : new(),
    };

    public ProductEditRequest ToRequest() => new(
        Name,
        Description,
        Price,
        StockQuantity,
        IsPreorder,
        ExpectedAvailabilityDate,
        HasCustomShipping ? Shipping.ToRate() : null);
}
