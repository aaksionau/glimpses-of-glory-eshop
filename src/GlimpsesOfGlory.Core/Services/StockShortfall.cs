namespace GlimpsesOfGlory.Core.Services;

// A product that can't cover what was wanted of it; Left is never negative.
public sealed record StockShortfall(int ProductId, string ProductName, int Wanted, int Left);
