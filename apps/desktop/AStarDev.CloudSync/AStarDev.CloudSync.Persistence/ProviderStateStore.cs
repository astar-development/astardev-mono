using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IProviderStateStore"/>. Each call uses its own short-lived context.</summary>
internal sealed class ProviderStateStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IProviderStateStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<SyncJobId, DateTimeOffset>, StoreError>> ListLastSyncAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var states = await context.ProviderStates.AsNoTracking().ToListAsync(token).ConfigureAwait(false);

            return (IReadOnlyDictionary<SyncJobId, DateTimeOffset>)states.Where(state => state.LastSyncUtc is not null).ToDictionary(state => state.SyncJobId, state => state.LastSyncUtc!.Value);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
