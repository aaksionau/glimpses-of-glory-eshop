namespace GlimpsesOfGlory.Domain;

public sealed class ProductPhoto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public required string FileName { get; set; }
    public int DisplayOrder { get; set; }
}
