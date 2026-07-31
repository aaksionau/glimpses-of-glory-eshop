namespace GlimpsesOfGlory.Domain;

public sealed class Cart
{
    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity => Lines.Sum(l => l.Quantity);

    public decimal Subtotal => Lines.Sum(l => l.UnitPrice * l.Quantity);

    public void SetLineQuantity(string productSlug, string productName, decimal unitPrice, string? thumbnailFileName, int quantity)
    {
        var existing = Lines.FirstOrDefault(l => l.ProductSlug == productSlug);

        if (quantity <= 0)
        {
            if (existing is not null)
            {
                Lines.Remove(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.ProductName = productName;
            existing.UnitPrice = unitPrice;
            existing.ThumbnailFileName = thumbnailFileName;
            existing.Quantity = quantity;
        }
        else
        {
            Lines.Add(new CartLine
            {
                ProductSlug = productSlug,
                ProductName = productName,
                UnitPrice = unitPrice,
                ThumbnailFileName = thumbnailFileName,
                Quantity = quantity,
            });
        }
    }

    public void RemoveLine(string productSlug) => Lines.RemoveAll(l => l.ProductSlug == productSlug);
}
