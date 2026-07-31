using GlimpsesOfGlory.Core.Orders.Entities;
using GlimpsesOfGlory.Core.Products.Entities;
using GlimpsesOfGlory.Core.Shipping.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPhoto> ProductPhotos => Set<ProductPhoto>();
    public DbSet<ShippingTierSetting> ShippingTiers => Set<ShippingTierSetting>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<PendingCheckout> PendingCheckouts => Set<PendingCheckout>();
    public DbSet<PendingCheckoutLine> PendingCheckoutLines => Set<PendingCheckoutLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
