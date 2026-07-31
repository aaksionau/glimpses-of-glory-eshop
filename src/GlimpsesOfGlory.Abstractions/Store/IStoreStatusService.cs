namespace GlimpsesOfGlory.Abstractions.Store;

public interface IStoreStatusService
{
    Task<string?> GetCurrentMessageAsync(CancellationToken cancellationToken);
}
