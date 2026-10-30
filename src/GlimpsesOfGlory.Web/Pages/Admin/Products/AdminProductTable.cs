using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

// The model for _ProductTable: the listed products and whether they're the low-stock subset.
public sealed record AdminProductTable(IReadOnlyList<AdminProductSummary> Products, bool LowStockOnly);
