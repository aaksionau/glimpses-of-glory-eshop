namespace GlimpsesOfGlory.Infrastructure.Products;

// Single source of truth for the placeholder photo filenames referenced by both
// AppDbContext's seed data and ProductPhotoSeeder, so the two can't drift apart.
internal static class ProductPhotoFileNames
{
    public const string SampleProductOnePhoto1 = "sample-product-one-1.svg";
    public const string SampleProductOnePhoto2 = "sample-product-one-2.svg";
    public const string SampleProductTwoPhoto1 = "sample-product-two-1.svg";
    public const string SampleProductTwoPhoto2 = "sample-product-two-2.svg";
    public const string SampleProductThreePhoto1 = "sample-product-three-1.svg";
    public const string SampleProductThreePhoto2 = "sample-product-three-2.svg";
}
