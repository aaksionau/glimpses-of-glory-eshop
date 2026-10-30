using System.Globalization;
using System.Text;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class OrderCsvExportTests
{
    private static readonly DateTimeOffset Placed = new(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Write_StartsWithAUtf8ByteOrderMark_AndAHeaderRow()
    {
        var bytes = await WriteAsync();

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(
            "Order number,Date (UTC),Customer name,Customer email,Fulfillment,Status,Payment method,Paid date (UTC),Subtotal,Shipping,Total\r\n",
            Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task Write_OneRowPerOrder_WithPlainNumberMoneyColumns()
    {
        var lines = await LinesAsync(
            Row(1, subtotal: 1234.5m, shipping: 0m, paidAt: new DateTimeOffset(2026, 10, 3, 14, 5, 0, TimeSpan.Zero)),
            Row(2, fulfillment: FulfillmentMethod.Pickup, status: OrderStatus.Shipped, payment: PaymentMethod.Cash, subtotal: 20m, shipping: 0m));

        Assert.Equal("1,2026-10-02 09:30,Jane Shopper,jane@example.com,Shipping,New,Card,2026-10-03 14:05,1234.50,0.00,1234.50", lines[1]);
        Assert.Equal("2,2026-10-02 09:30,Jane Shopper,jane@example.com,Pickup,Ready for pickup,Cash,,20.00,0.00,20.00", lines[2]);
    }

    [Fact]
    public async Task Write_MoneyIgnoresTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var lines = await LinesAsync(Row(1, subtotal: 1234.5m, shipping: 7.25m));

            Assert.EndsWith(",1234.50,7.25,1241.75", lines[1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Write_QuotesFieldsWithCommasQuotesAndLineBreaks()
    {
        var lines = await LinesAsync(Row(1, name: "Doe, \"JJ\"\nJane"));

        Assert.Contains("\"Doe, \"\"JJ\"\"\nJane\"", string.Join("\r\n", lines));
    }

    [Fact]
    public async Task Write_KeepsAccentedNamesIntact()
    {
        var lines = await LinesAsync(Row(1, name: "Zoë Łukasiewicz"));

        Assert.Contains(",Zoë Łukasiewicz,", lines[1]);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("＝SUM(A1)", "'＝SUM(A1)")]
    public async Task Write_NeutralizesFormulasInCustomerText(string name, string expected)
    {
        var lines = await LinesAsync(Row(1, name: name));

        Assert.Contains($",{expected},", lines[1]);
    }

    [Fact]
    public void FileName_NamesTheRange()
    {
        Assert.Equal("orders-2026-10-01-to-2026-10-31.csv", OrderCsvExport.FileName(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void DefaultRange_IsTheCurrentUtcMonthSoFar()
    {
        // 7 pm on Oct 30 in UTC-5 is already Oct 31 in UTC.
        Assert.Equal(
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
            OrderCsvExport.DefaultRange(new DateTimeOffset(2026, 10, 30, 19, 0, 0, TimeSpan.FromHours(-5))));
    }

    private static AdminOrderExportRow Row(
        int id,
        string name = "Jane Shopper",
        FulfillmentMethod fulfillment = FulfillmentMethod.Shipping,
        OrderStatus status = OrderStatus.New,
        PaymentMethod payment = PaymentMethod.Card,
        DateTimeOffset? paidAt = null,
        decimal subtotal = 10m,
        decimal shipping = 5m) =>
        new(id, Placed, name, "jane@example.com", fulfillment, status, payment, paidAt, subtotal, shipping);

    private static async Task<string[]> LinesAsync(params AdminOrderExportRow[] rows) =>
        Encoding.UTF8.GetString((await WriteAsync(rows))[3..]).Split("\r\n");

    private static async Task<byte[]> WriteAsync(params AdminOrderExportRow[] rows)
    {
        using var output = new MemoryStream();
        await OrderCsvExport.WriteAsync(rows.ToAsyncEnumerable(), output, CancellationToken.None);
        return output.ToArray();
    }
}
