using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GlimpsesOfGlory.IntegrationTests;

// Forces a race: each participating context is held after its first query returns until every
// participant has run its first query. Two admins acting on one order therefore both read its
// status before either of them writes - the window the status guard has to cover.
internal sealed class FirstQueryRendezvous(int participants)
{
    private readonly TaskCompletionSource allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrived;

    public IInterceptor CreateInterceptor() => new Interceptor(this);

    private async Task ArriveAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref arrived) == participants)
        {
            allArrived.TrySetResult();
        }
        await allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    private sealed class Interceptor(FirstQueryRendezvous rendezvous) : DbCommandInterceptor
    {
        private int seenFirstQuery;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref seenFirstQuery, 1) == 0)
            {
                await rendezvous.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }
}
