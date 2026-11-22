using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IItemIndexStore"/>. Each call uses its own short-lived context, so no tracked entity outlives a page and memory stays flat however many items are crawled.</summary>
internal sealed class ItemIndexStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IItemIndexStore
{
    /// <inheritdoc/>
    public async Task<Result<CrawlCheckpoint, StoreError>> GetCheckpointAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entity = await context.CrawlCheckpoints.AsNoTracking().SingleOrDefaultAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false);

            return entity?.ToCheckpoint() ?? CrawlCheckpoint.None;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> SavePageAsync(SyncScope scope, IReadOnlyList<ProviderItem> items, CrawlCheckpoint checkpoint, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncScopes.AnyAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncScopeNotFound(scope));

            await UpsertItemsAsync(context, scope.SyncJobId, items, observedUtc, token).ConfigureAwait(false);
            await UpsertCheckpointAsync(context, scope, checkpoint, observedUtc, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    private static async Task UpsertItemsAsync(CloudSyncDbContext context, SyncJobId jobId, IReadOnlyList<ProviderItem> items, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var remoteIds = items.Select(item => item.RemoteItemId).Distinct().ToList();
        var known = (await context.Items.Where(row => row.SyncJobId == jobId && remoteIds.Contains(row.RemoteItemId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(row => row.RemoteItemId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var item in items)
        {
            if (known.TryGetValue(item.RemoteItemId, out var existing))
            {
                existing.LocalPath = item.Path;
                existing.UpdatedUtc = observedUtc;
            }
            else
            {
                existing = new ItemEntity { Id = new ItemId(Guid.NewGuid()), SyncJobId = jobId, ItemType = item.ItemType, RemoteItemId = item.RemoteItemId, LocalPath = item.Path, CreatedUtc = observedUtc, UpdatedUtc = observedUtc };
                known[item.RemoteItemId] = existing;
                context.Items.Add(existing);
            }

            context.ItemSnapshots.Add(new ItemSnapshotEntity { Id = new SnapshotId(Guid.NewGuid()), ItemId = existing.Id, Size = item.Size, ModifiedUtc = item.ModifiedUtc, ObservedUtc = observedUtc, SnapshotSource = SnapshotSource.Remote });
        }
    }

    private static async Task UpsertCheckpointAsync(CloudSyncDbContext context, SyncScope scope, CrawlCheckpoint checkpoint, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var row = await context.CrawlCheckpoints.SingleOrDefaultAsync(entity => entity.SyncJobId == scope.SyncJobId && entity.RemoteRootId == scope.RemoteRootId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new CrawlCheckpointEntity { SyncJobId = scope.SyncJobId, RemoteRootId = scope.RemoteRootId };
            context.CrawlCheckpoints.Add(row);
        }

        row.ResumeToken = checkpoint.ResumeToken;
        row.ItemsIndexed = checkpoint.ItemsIndexed;
        row.Completed = checkpoint.Completed;
        row.DeltaToken = checkpoint.DeltaToken;
        row.UpdatedUtc = observedUtc;
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
