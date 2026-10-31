using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Infrastructure.Options;
using GlimpsesOfGlory.Infrastructure.Services;

// Composition root, part of Program (see Program.cs): the storefront catalog and product photo storage.
public partial class Program
{
    private static void AddCatalog(WebApplicationBuilder builder, string productPhotosPath)
    {
        builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
        builder.Services.AddScoped<IProductPhotoStorage, ProductPhotoStorage>();
        builder.Services.AddSingleton(new ProductPhotoStorageOptions { StoragePath = productPhotosPath });
    }

    // Local-disk product photo storage. In production this path should be a
    // Dokploy-mounted persistent volume (set via ProductPhotos__StoragePath),
    // separate from wwwroot so uploaded photos survive redeploys.
    private static string ResolveProductPhotosPath(WebApplicationBuilder builder)
    {
        var configuredPhotosPath = builder.Configuration["ProductPhotos:StoragePath"];
        return Path.GetFullPath(
            string.IsNullOrEmpty(configuredPhotosPath) ? "App_Data/product-photos" : configuredPhotosPath,
            builder.Environment.ContentRootPath);
    }
}
