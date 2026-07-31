using GlimpsesOfGlory.Domain;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StoreStatus> StoreStatuses => Set<StoreStatus>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPhoto> ProductPhotos => Set<ProductPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoreStatus>().HasData(new StoreStatus
        {
            Id = 1,
            Message = "Glimpses of Glory is under construction.",
            UpdatedAtUtc = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.Property(p => p.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<ProductPhoto>()
            .HasOne<Product>()
            .WithMany(p => p.Photos)
            .HasForeignKey(photo => photo.ProductId);

        // Placeholder seed data - swap for real products via admin CRUD once it exists.
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = 1,
                Name = "Sample Product One",
                Slug = "sample-product-one",
                Description = "Placeholder description for sample product one. Replace with real product copy.",
                Price = 19.99m,
                StockQuantity = 10,
            },
            new Product
            {
                Id = 2,
                Name = "Sample Product Two",
                Slug = "sample-product-two",
                Description = "Placeholder description for sample product two. Replace with real product copy.",
                Price = 29.99m,
                StockQuantity = 5,
            },
            new Product
            {
                Id = 3,
                Name = "Sample Product Three",
                Slug = "sample-product-three",
                Description = "Placeholder description for sample product three. Replace with real product copy.",
                Price = 39.99m,
                StockQuantity = 8,
            });

        modelBuilder.Entity<ProductPhoto>().HasData(
            new ProductPhoto { Id = 1, ProductId = 1, FileName = "sample-product-one-1.svg", DisplayOrder = 1 },
            new ProductPhoto { Id = 2, ProductId = 1, FileName = "sample-product-one-2.svg", DisplayOrder = 2 },
            new ProductPhoto { Id = 3, ProductId = 2, FileName = "sample-product-two-1.svg", DisplayOrder = 1 },
            new ProductPhoto { Id = 4, ProductId = 2, FileName = "sample-product-two-2.svg", DisplayOrder = 2 },
            new ProductPhoto { Id = 5, ProductId = 3, FileName = "sample-product-three-1.svg", DisplayOrder = 1 },
            new ProductPhoto { Id = 6, ProductId = 3, FileName = "sample-product-three-2.svg", DisplayOrder = 2 });
    }
}
