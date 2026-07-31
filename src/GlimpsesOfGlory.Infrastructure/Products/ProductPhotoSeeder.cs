namespace GlimpsesOfGlory.Infrastructure.Products;

// Writes the placeholder photos referenced by AppDbContext's seed data to the
// configured local-disk storage path, since EF Core migrations only seed rows,
// not files. Replace with real product photos (via admin CRUD, once it exists)
// and this seeder becomes a no-op (existing files are never overwritten).
public static class ProductPhotoSeeder
{
    private static readonly (string FileName, string Color, string Label)[] PlaceholderPhotos =
    [
        ("sample-product-one-1.svg", "#b45309", "Sample Product One - Photo 1"),
        ("sample-product-one-2.svg", "#92400e", "Sample Product One - Photo 2"),
        ("sample-product-two-1.svg", "#1d4ed8", "Sample Product Two - Photo 1"),
        ("sample-product-two-2.svg", "#1e40af", "Sample Product Two - Photo 2"),
        ("sample-product-three-1.svg", "#15803d", "Sample Product Three - Photo 1"),
        ("sample-product-three-2.svg", "#166534", "Sample Product Three - Photo 2"),
    ];

    public static void EnsureSeeded(string storagePath)
    {
        Directory.CreateDirectory(storagePath);

        foreach (var (fileName, color, label) in PlaceholderPhotos)
        {
            var filePath = Path.Combine(storagePath, fileName);
            if (File.Exists(filePath))
            {
                continue;
            }

            File.WriteAllText(filePath, BuildPlaceholderSvg(color, label));
        }
    }

    private static string BuildPlaceholderSvg(string color, string label) => $"""
        <svg xmlns="http://www.w3.org/2000/svg" width="800" height="600" viewBox="0 0 800 600">
          <rect width="800" height="600" fill="{color}" />
          <text x="400" y="300" fill="white" font-family="sans-serif" font-size="28" text-anchor="middle" dominant-baseline="middle">{label}</text>
        </svg>
        """;
}
