using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record AdminOrderTabCounts(int NeedsAction, int Shipped, int Cancelled, int All)
{
    public int For(AdminOrderTab tab) => tab switch
    {
        AdminOrderTab.NeedsAction => NeedsAction,
        AdminOrderTab.Shipped => Shipped,
        AdminOrderTab.Cancelled => Cancelled,
        AdminOrderTab.All => All,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
    };
}
