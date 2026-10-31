namespace GlimpsesOfGlory.Abstractions.Services;

// Where admin-uploaded product photo files live. The storefront serves them from the same place
// (see Program.cs's /product-photos static file mapping), so only the file name is stored.
public interface IProductPhotoStorage
{
    // Stores the content under a new unique file name (keeping the original extension) and returns that name.
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken);

    // Removes the file; a file that is already gone is not an error.
    void Delete(string fileName);
}
