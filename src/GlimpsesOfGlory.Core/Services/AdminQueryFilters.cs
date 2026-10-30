using System.Linq.Expressions;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// SQL-translatable filters shared by the admin lists and the admin dashboard, so a dashboard
// number always matches the list it links to.
internal static class AdminQueryFilters
{
    // Placed but not yet shipped, ready for pickup, or cancelled.
    public static readonly Expression<Func<Order, bool>> ToFulfill =
        o => o.Status == OrderStatus.New;

    // Cash still to be collected on an order that hasn't been cancelled.
    public static readonly Expression<Func<Order, bool>> UnpaidCash =
        o => o.PaymentMethod == PaymentMethod.Cash && o.PaidAt == null && o.Status != OrderStatus.Cancelled;

    // Visible, stock-tracked products running out. Preorder products have no stock ceiling.
    public static readonly Expression<Func<Product, bool>> LowStock =
        p => p.IsActive && !p.IsPreorder && p.StockQuantity <= StockPolicy.LowStockThreshold;
}
