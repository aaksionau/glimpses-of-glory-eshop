using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;

// Composition root, part of Program (see Program.cs): the admin back office —
// product management, order management and the dashboard.
public partial class Program
{
    private static void AddAdmin(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAdminProductService, AdminProductService>();
        builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
        builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
    }
}
