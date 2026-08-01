using System.ComponentModel.DataAnnotations;

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
}
