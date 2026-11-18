using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.Core.Services;

// Pure domain service: rates are resolved by the caller (per product, or the store default),
// no I/O happens here.
//
// One parcel: the item with the highest first-item rate pays that rate, and every other unit
// in the cart pays its product's additional-item rate. On a tie for the highest first-item rate,
// the product with the higher additional rate opens the parcel, so the shopper pays the least.
public static class ShippingCalculator
{
    public static decimal Calculate(IEnumerable<ShippingItem> items)
    {
        var shipped = items.Where(i => i.Quantity > 0).ToList();
        if (shipped.Count == 0)
        {
            return 0m;
        }

        var opener = shipped
            .OrderByDescending(i => i.Rate.FirstItem)
            .ThenByDescending(i => i.Rate.AdditionalItem)
            .First();

        var allAdditional = shipped.Sum(i => i.Rate.AdditionalItem * i.Quantity);
        return opener.Rate.FirstItem + allAdditional - opener.Rate.AdditionalItem;
    }
}
