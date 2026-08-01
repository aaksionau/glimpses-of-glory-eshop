using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GlimpsesOfGlory.Core.Orders.Persistence;

internal static class DbUpdateExceptionExtensions
{
    // Stripe redelivers webhooks (e.g. on timeout), so two concurrent webhook deliveries for
    // the same PaymentIntent can race to insert the Order; the unique index on
    // Orders.StripePaymentIntentId is what actually prevents the duplicate, and this just
    // recognizes that failure so the loser can treat it as an idempotent no-op instead of
    // an error.
    public static bool IsDuplicateStripePaymentIntentId(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == OrderConfiguration.StripePaymentIntentIdIndexName;
}
