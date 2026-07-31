namespace GlimpsesOfGlory.Infrastructure.Products;

// Writes the placeholder photos referenced by AppDbContext's seed data to the
// configured local-disk storage path, since EF Core migrations only seed rows,
// not files. Replace with real product photos (via admin CRUD, once it exists)
// and this seeder becomes a no-op (existing files are never overwritten).
public static class ProductPhotoSeeder
{
    private static readonly (string FileName, string Color, string Label)[] PlaceholderPhotos =
    [
        (ProductPhotoFileNames.SampleProductOnePhoto1, "#b45309", "Sample Product One - Photo 1"),
        (ProductPhotoFileNames.SampleProductOnePhoto2, "#92400e", "Sample Product One - Photo 2"),
        (ProductPhotoFileNames.SampleProductTwoPhoto1, "#1d4ed8", "Sample Product Two - Photo 1"),
        (ProductPhotoFileNames.SampleProductTwoPhoto2, "#1e40af", "Sample Product Two - Photo 2"),
        (ProductPhotoFileNames.SampleProductThreePhoto1, "#15803d", "Sample Product Three - Photo 1"),
        (ProductPhotoFileNames.SampleProductThreePhoto2, "#166534", "Sample Product Three - Photo 2"),
    ];

    public static async Task EnsureSeededAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(storagePath);

        var existingFiles = new HashSet<string>(
            Directory.EnumerateFiles(storagePath).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        var writes = PlaceholderPhotos
            .Where(photo => !existingFiles.Contains(photo.FileName))
            .Select(photo => File.WriteAllTextAsync(
                Path.Combine(storagePath, photo.FileName),
                BuildPlaceholderSvg(photo.Color, photo.Label),
                cancellationToken));

        await Task.WhenAll(writes);
    }

    private static string BuildPlaceholderSvg(string color, string label) => $"""
        <svg xmlns="http://www.w3.org/2000/svg" width="800" height="600" viewBox="0 0 800 600">
          <rect width="800" height="600" fill="{color}" />
          <text x="400" y="300" fill="white" font-family="sans-serif" font-size="28" text-anchor="middle" dominant-baseline="middle">{label}</text>
        </svg>
        """;
}
