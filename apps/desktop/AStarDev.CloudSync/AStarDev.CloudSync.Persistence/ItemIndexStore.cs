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

            if (entity is null) return CrawlCheckpoint.None;

            var state = await context.DeltaStates.AsNoTracking().SingleOrDefaultAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false);

            return entity.ToCheckpoint(state?.DeltaToken);
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

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> ResetScopeAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncScopes.AnyAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncScopeNotFound(scope));

            await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
            await context.CrawlCheckpoints.Where(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId).ExecuteDeleteAsync(token).ConfigureAwait(false);
            await context.DeltaStates.Where(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId).ExecuteDeleteAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> ApplyDeltaPageAsync(SyncScope scope, IReadOnlyList<ProviderChange> changes, DeltaToken? newToken, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncScopes.AnyAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncScopeNotFound(scope));

            await ApplyChangesAsync(context, scope.SyncJobId, changes, observedUtc, token).ConfigureAwait(false);
            if (newToken is { } fresh) await UpsertDeltaStateAsync(context, scope, fresh, observedUtc, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    private static async Task<Dictionary<RemoteItemId, ItemEntity>> LoadKnownAsync(CloudSyncDbContext context, SyncJobId jobId, IEnumerable<RemoteItemId> ids, CancellationToken cancellationToken)
    {
        var remoteIds = ids.Distinct().ToList();

        return (await context.Items.Where(row => row.SyncJobId == jobId && EF.Parameter(remoteIds).Contains(row.RemoteItemId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(row => row.RemoteItemId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static async Task UpsertItemsAsync(CloudSyncDbContext context, SyncJobId jobId, IReadOnlyList<ProviderItem> items, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var known = await LoadKnownAsync(context, jobId, items.Select(item => item.RemoteItemId), cancellationToken).ConfigureAwait(false);
        var snapshots = await RemoteSnapshotsAsync(context, known, cancellationToken).ConfigureAwait(false);

        foreach (var item in items) Upsert(context, jobId, known, snapshots, item, observedUtc);
    }

    private static Task<Dictionary<ItemId, ItemSnapshotEntity>> RemoteSnapshotsAsync(CloudSyncDbContext context, Dictionary<RemoteItemId, ItemEntity> known, CancellationToken cancellationToken)
        => ItemSnapshotRows.LoadAsync(context, [.. known.Values.Select(item => item.Id)], SnapshotSource.Remote, cancellationToken);

    private static void Upsert(CloudSyncDbContext context, SyncJobId jobId, Dictionary<RemoteItemId, ItemEntity> known, Dictionary<ItemId, ItemSnapshotEntity> snapshots, ProviderItem item, DateTimeOffset observedUtc)
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

        ItemSnapshotRows.Observe(context, snapshots, existing.Id, SnapshotSource.Remote, item.Size, item.ModifiedUtc, null, null, observedUtc);
    }

    private static async Task ApplyChangesAsync(CloudSyncDbContext context, SyncJobId jobId, IReadOnlyList<ProviderChange> changes, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var known = await LoadKnownAsync(context, jobId, changes.Select(change => change.RemoteItemId), cancellationToken).ConfigureAwait(false);
        var snapshots = await RemoteSnapshotsAsync(context, known, cancellationToken).ConfigureAwait(false);

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case ChangeKind.Created:
                case ChangeKind.Modified:
                    if (change.Item is { } changed) Upsert(context, jobId, known, snapshots, changed, observedUtc);
                    break;
                case ChangeKind.Renamed:
                    if (change.Item is not { } renamed) break;
                    if (known.TryGetValue(renamed.RemoteItemId, out var existing))
                    {
                        existing.LocalPath = renamed.Path;
                        existing.UpdatedUtc = observedUtc;
                    }
                    else Upsert(context, jobId, known, snapshots, renamed, observedUtc);
                    break;
                case ChangeKind.Deleted:
                    if (known.Remove(change.RemoteItemId, out var gone)) await RemoveWithDescendantsAsync(context, known, gone, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private static async Task RemoveWithDescendantsAsync(CloudSyncDbContext context, Dictionary<RemoteItemId, ItemEntity> known, ItemEntity root, CancellationToken cancellationToken)
    {
        var doomed = new List<ItemEntity> { root };
        var frontier = new List<ItemId?> { root.Id };

        while (frontier.Count > 0)
        {
            var parents = frontier;
            var children = await context.Items.Where(row => parents.Contains(row.ParentItemId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            doomed.AddRange(children);
            frontier = children.Select(child => (ItemId?)child.Id).ToList();
        }

        var doomedIds = doomed.Select(item => item.Id).ToHashSet();
        foreach (var pending in context.ChangeTracker.Entries<ItemSnapshotEntity>().Where(entry => entry.State == EntityState.Added && doomedIds.Contains(entry.Entity.ItemId)).ToList()) pending.State = EntityState.Detached;

        foreach (var stale in known.Where(pair => doomedIds.Contains(pair.Value.Id)).Select(pair => pair.Key).ToList()) known.Remove(stale);

        context.Items.RemoveRange(doomed);
    }

    private static async Task UpsertDeltaStateAsync(CloudSyncDbContext context, SyncScope scope, DeltaToken token, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var row = await context.DeltaStates.SingleOrDefaultAsync(entity => entity.SyncJobId == scope.SyncJobId && entity.RemoteRootId == scope.RemoteRootId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new DeltaStateEntity { SyncJobId = scope.SyncJobId, RemoteRootId = scope.RemoteRootId };
            context.DeltaStates.Add(row);
        }

        row.DeltaToken = token;
        row.UpdatedUtc = observedUtc;
        await UpsertProviderStateAsync(context, scope.SyncJobId, token, observedUtc, cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertProviderStateAsync(CloudSyncDbContext context, SyncJobId syncJobId, DeltaToken token, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var state = await context.ProviderStates.SingleOrDefaultAsync(entity => entity.SyncJobId == syncJobId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            state = new ProviderStateEntity { SyncJobId = syncJobId };
            context.ProviderStates.Add(state);
        }

        state.DeltaToken = token;
        state.LastSyncUtc = observedUtc;
        state.LastScanUtc = observedUtc;
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
        row.UpdatedUtc = observedUtc;
        if (checkpoint.DeltaToken is { } token) await UpsertDeltaStateAsync(context, scope, token, observedUtc, cancellationToken).ConfigureAwait(false);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
