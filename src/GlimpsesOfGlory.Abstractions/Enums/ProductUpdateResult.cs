namespace GlimpsesOfGlory.Abstractions.Enums;

public enum ProductUpdateResult
{
    Updated,
    NotFound,
    // The admin changed the stock field, but stock had moved (e.g. an order came in) since the form
    // was loaded. Nothing was saved.
    StockChanged,
}
