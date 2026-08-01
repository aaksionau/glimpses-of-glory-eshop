namespace GlimpsesOfGlory.Abstractions.Shipping;

public interface IShippingSettingsService
{
    Task<IReadOnlyList<ShippingTierView>> GetTiersAsync(CancellationToken cancellationToken);

    Task UpdateTiersAsync(IReadOnlyList<ShippingTierUpdate> tiers, CancellationToken cancellationToken);
}

public sealed record ShippingTierView(int Id, int MinQuantity, decimal Amount);

public sealed record ShippingTierUpdate(int Id, int MinQuantity, decimal Amount);
