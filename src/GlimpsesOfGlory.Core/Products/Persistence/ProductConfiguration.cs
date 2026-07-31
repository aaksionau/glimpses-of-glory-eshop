using GlimpsesOfGlory.Core.Products.Entities;
using GlimpsesOfGlory.Core.Products.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Products.Persistence;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Price).HasPrecision(10, 2);

        // Placeholder seed data - swap for real products via admin CRUD once it exists.
        builder.HasData(
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
    }
}

internal sealed class ProductPhotoConfiguration : IEntityTypeConfiguration<ProductPhoto>
{
    public void Configure(EntityTypeBuilder<ProductPhoto> builder)
    {
        builder.HasOne<Product>()
            .WithMany(p => p.Photos)
            .HasForeignKey(photo => photo.ProductId);

        builder.HasData(
            new ProductPhoto { Id = 1, ProductId = 1, FileName = ProductPhotoFileNames.SampleProductOnePhoto1, DisplayOrder = 1 },
            new ProductPhoto { Id = 2, ProductId = 1, FileName = ProductPhotoFileNames.SampleProductOnePhoto2, DisplayOrder = 2 },
            new ProductPhoto { Id = 3, ProductId = 2, FileName = ProductPhotoFileNames.SampleProductTwoPhoto1, DisplayOrder = 1 },
            new ProductPhoto { Id = 4, ProductId = 2, FileName = ProductPhotoFileNames.SampleProductTwoPhoto2, DisplayOrder = 2 },
            new ProductPhoto { Id = 5, ProductId = 3, FileName = ProductPhotoFileNames.SampleProductThreePhoto1, DisplayOrder = 1 },
            new ProductPhoto { Id = 6, ProductId = 3, FileName = ProductPhotoFileNames.SampleProductThreePhoto2, DisplayOrder = 2 });
    }
}
