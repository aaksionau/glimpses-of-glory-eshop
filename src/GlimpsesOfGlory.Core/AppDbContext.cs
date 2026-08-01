using GlimpsesOfGlory.Core.Products.Entities;
using GlimpsesOfGlory.Core.Shipping.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPhoto> ProductPhotos => Set<ProductPhoto>();
    public DbSet<ShippingTierSetting> ShippingTiers => Set<ShippingTierSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
