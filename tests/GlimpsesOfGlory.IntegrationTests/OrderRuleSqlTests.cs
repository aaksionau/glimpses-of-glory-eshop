using GlimpsesOfGlory.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// The SQL form of every OrderLifecycle rule must pick exactly the orders its in-memory form accepts,
// over every combination of facts - this is what keeps dashboard counts and list tabs in step with
// the badges. The in-memory forms are tested in GlimpsesOfGlory.UnitTests' OrderLifecycleTests.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OrderRuleSqlTests(PostgresFixture postgres) : IAsyncLifetime
{
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
            seed.Orders.AddRange(OrderLifecycleRules.AllFacts().Select(OrderLifecycleRules.OrderWith));
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDb();
        var orders = await db.Orders.AsNoTracking().ToListAsync();
        var inMemory = orders.Where(o => rule.IsSatisfiedBy(OrderFacts.Of(o))).Select(o => o.Id).Order().ToList();
        var inSql = await db.Orders.Where(rule.OnOrders).Select(o => o.Id).OrderBy(id => id).ToListAsync();

        Assert.Equal(inMemory, inSql);
    }
}
