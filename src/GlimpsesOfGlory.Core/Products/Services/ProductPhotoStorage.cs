namespace GlimpsesOfGlory.Core.Products.Services;

// Writes admin-uploaded product photos to the same local-disk volume the
// storefront serves from (see Program.cs's /product-photos static file mapping).
public sealed class ProductPhotoStorage(ProductPhotoStorageOptions options)
{
    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.StoragePath);

        var extension = Path.GetExtension(originalFileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(options.StoragePath, fileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return fileName;
    }

    public void Delete(string fileName)
    {
        var fullPath = Path.Combine(options.StoragePath, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
