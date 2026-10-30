namespace GlimpsesOfGlory.Abstractions.Dtos;

// One page of the admin orders list. Page is 1-based and is the page actually returned, which
// may differ from the one asked for when that was out of range.
public sealed record AdminOrderPage(IReadOnlyList<AdminOrderSummary> Orders, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageCount(TotalCount, PageSize);

    // An empty list still has one (empty) page.
    public static int PageCount(int totalCount, int pageSize) => Math.Max(1, (totalCount + pageSize - 1) / pageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    // 1-based positions of the first and last order on this page, for "Showing 26-50 of 120".
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => (Page - 1) * PageSize + Orders.Count;
}
