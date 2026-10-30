namespace GlimpsesOfGlory.Abstractions.Dtos;

// What needs the shop owner's attention, as shown on the admin dashboard.
// Revenue counts paid, non-cancelled orders by the time they were paid.
public sealed record AdminDashboardMetrics(
    int OrdersToFulfill,
    int UnpaidCashOrders,
    int LowStockProducts,
    decimal RevenueLast7Days,
    decimal RevenueLast30Days);
