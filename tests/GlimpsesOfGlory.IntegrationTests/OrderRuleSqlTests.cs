using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// The SQL form of every OrderLifecycle rule must pick exactly the orders its in-memory form accepts,
// over every combination of facts - this is what keeps dashboard counts and list tabs in step with
// the badges. The in-memory forms are tested in GlimpsesOfGlory.UnitTests' OrderLifecycleTests.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OrderRuleSqlTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly bool[] BothPaidStates = [false, true];

    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    public static TheoryData<string> Rules() => new(OrderLifecycleRules.ByName.Keys);

    [Theory]
    [MemberData(nameof(Rules))]
    public async Task OnOrders_MatchesIsSatisfiedBy_ForEveryCombinationOfFacts(string ruleName)
    {
        var rule = OrderLifecycleRules.ByName[ruleName];
        await using (var seed = database.CreateDb())
        {
            seed.Orders.AddRange(
                from status in Enum.GetValues<OrderStatus>()
                from fulfillmentMethod in Enum.GetValues<FulfillmentMethod>()
                from paymentMethod in Enum.GetValues<PaymentMethod>()
                from isPaid in BothPaidStates
                select NewOrder(status, fulfillmentMethod, paymentMethod, isPaid));
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDb();
        var orders = await db.Orders.AsNoTracking().ToListAsync();
        var inMemory = orders.Where(o => rule.IsSatisfiedBy(OrderFacts.Of(o))).Select(o => o.Id).Order().ToList();
        var inSql = await db.Orders.Where(rule.OnOrders).Select(o => o.Id).OrderBy(id => id).ToListAsync();

        Assert.Equal(inMemory, inSql);
    }

    private static Order NewOrder(OrderStatus status, FulfillmentMethod fulfillmentMethod, PaymentMethod paymentMethod, bool isPaid) => new()
    {
        Email = "shopper@example.com",
        ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
        Status = status,
        FulfillmentMethod = fulfillmentMethod,
        PaymentMethod = paymentMethod,
        PaidAt = isPaid ? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero) : null,
    };
}
