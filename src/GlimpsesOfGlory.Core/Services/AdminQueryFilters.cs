using System.Linq.Expressions;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// SQL-translatable filters shared by the admin lists and the admin dashboard, so a dashboard
// number always matches the list it links to. Order filters come from OrderLifecycle's rules.
internal static class AdminQueryFilters
{
    // Visible, stock-tracked products running out. Preorder products have no stock ceiling.
    public static readonly Expression<Func<Product, bool>> LowStock =
        p => p.IsActive && !p.IsPreorder && p.StockQuantity <= StockPolicy.LowStockThreshold;
}
