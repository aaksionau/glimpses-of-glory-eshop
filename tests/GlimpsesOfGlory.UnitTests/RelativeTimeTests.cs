using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(30, "just now")]
    [InlineData(-120, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(2 * 3600, "2 h ago")]
    [InlineData(23 * 3600 + 59 * 60, "23 h ago")]
    [InlineData(24 * 3600, "1 d ago")]
    [InlineData(6 * 86400, "6 d ago")]
    [InlineData(7 * 86400, "Oct 22, 2026")]
    public void Format_DescribesHowLongAgo(int secondsAgo, string expected)
    {
        Assert.Equal(expected, RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now));
    }

    [Fact]
    public void FullTimestamp_IsInUtc()
    {
        var at = new DateTimeOffset(2026, 10, 29, 14, 5, 0, TimeSpan.FromHours(2));

        Assert.Equal("October 29, 2026 at 12:05 UTC", RelativeTime.FullTimestamp(at));
    }
}
