namespace GlimpsesOfGlory.Core.Services;

// Sends an email whose failure mustn't undo what the caller has already committed (an order, a
// status change): the exception goes to logFailure instead of being thrown. Returns whether the
// send succeeded.
internal static class BestEffortEmail
{
    public static async Task<bool> TrySendAsync(Func<Task> send, Action<Exception> logFailure, CancellationToken cancellationToken)
    {
        try
        {
            await send();
            return true;
        }
        // Filtered on the caller's token, not the exception type: an HttpClient timeout is also an
        // OperationCanceledException and must be logged as a failed send, not escape as a 500.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logFailure(ex);
            return false;
        }
    }
}
