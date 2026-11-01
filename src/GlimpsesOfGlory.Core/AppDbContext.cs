using GlimpsesOfGlory.Core.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPhoto> ProductPhotos => Set<ProductPhoto>();
    public DbSet<ShippingTierSetting> ShippingTiers => Set<ShippingTierSetting>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<PendingCheckout> PendingCheckouts => Set<PendingCheckout>();
    public DbSet<PendingCheckoutLine> PendingCheckoutLines => Set<PendingCheckoutLine>();

    // ASP.NET Core Data Protection key ring, persisted here so auth/session cookies survive
    // container restarts and redeploys.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
