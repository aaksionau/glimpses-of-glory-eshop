using System.Globalization;
using System.Text;
using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

// Writes the bookkeeping CSV export: RFC 4180 quoting, CRLF line endings, and a UTF-8 byte-order
// mark so Excel opens accented names correctly. Money columns are plain invariant numbers
// ("1234.50") so a spreadsheet can sum them; timestamps are UTC.
public static class OrderCsvExport
{
    public const string ContentType = "text/csv; charset=utf-8";

    // Both bounds are inclusive, matching the range the export covers.
    public static (DateOnly From, DateOnly To) DefaultRange(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        return (new DateOnly(today.Year, today.Month, 1), today);
    }

    public static string FileName(DateOnly from, DateOnly to) =>
        string.Create(CultureInfo.InvariantCulture, $"orders-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv");

    private const char ByteOrderMark = (char)0xFEFF;

    private static readonly string[] Header =
    [
        "Order number", "Date (UTC)", "Customer name", "Customer email", "Fulfillment", "Status",
        "Payment method", "Paid date (UTC)", "Subtotal", "Shipping", "Total",
    ];

    // Rows are written as they arrive, so the whole export is never held in memory.
    public static async Task WriteAsync(IAsyncEnumerable<AdminOrderExportRow> rows, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.NewLine = "\r\n";
        await writer.WriteAsync(ByteOrderMark);
        await WriteLineAsync(writer, Header);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await WriteLineAsync(writer,
            [
                row.Id.ToString(CultureInfo.InvariantCulture),
                Timestamp(row.CreatedAt),
                Text(row.CustomerName),
                Text(row.Email),
                FulfillmentLabels.CostLabel(row.FulfillmentMethod),
                FulfillmentLabels.Status(row.Status, row.FulfillmentMethod),
                PaymentLabels.Method(row.PaymentMethod),
                row.PaidAt is { } paidAt ? Timestamp(paidAt) : "",
                Money(row.Subtotal),
                Money(row.ShippingCost),
                Money(row.Total),
            ]);
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static Task WriteLineAsync(StreamWriter writer, IEnumerable<string> fields) =>
        writer.WriteLineAsync(string.Join(',', fields.Select(Escape)));

    // Quoted only when needed: a comma, quote or line break in the value; quotes are doubled.
    private static string Escape(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    // Customer-entered text starting with a formula character would run as a formula in Excel
    // (CSV injection), so it is prefixed with an apostrophe to keep it literal.
    private static string Text(string value) =>
        value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;

    private static string Timestamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
