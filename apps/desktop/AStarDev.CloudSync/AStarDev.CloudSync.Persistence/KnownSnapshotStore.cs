using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IKnownSnapshotStore"/>. A commit applies the staged change and deletes it in one save, so a crash leaves either both or neither.</summary>
internal sealed class KnownSnapshotStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IKnownSnapshotStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<KnownEntry>, StoreError>> ListAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<KnownEntry>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.KnownSnapshots.AsNoTracking().Where(row => row.SyncJobId == jobId).ToListAsync(token).ConfigureAwait(false);

            return rows.Select(row => new KnownEntry(row.ItemId, row.ToSnapshot())).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> StageAsync(SyncJobId jobId, OperationId operationId, ItemId itemId, KnownSnapshot? target, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Unit>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            await context.StagedKnownSnapshots.Where(row => row.OperationId == operationId).ExecuteDeleteAsync(token).ConfigureAwait(false);
            context.StagedKnownSnapshots.Add(ToStaged(jobId, operationId, itemId, target));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> CommitAsync(OperationId operationId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Unit>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var staged = await context.StagedKnownSnapshots.SingleOrDefaultAsync(row => row.OperationId == operationId, token).ConfigureAwait(false);
            if (staged is null) return default;

            if (staged.Remove) await RemoveBaselineAndRowsAsync(context, staged.SyncJobId, staged.ItemId, token).ConfigureAwait(false);
            else await ReplaceBaselineAsync(context, staged, token).ConfigureAwait(false);

            context.StagedKnownSnapshots.Remove(staged);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static async Task ReplaceBaselineAsync(CloudSyncDbContext context, StagedKnownEntity staged, CancellationToken cancellationToken)
    {
        var current = await context.KnownSnapshots.SingleOrDefaultAsync(row => row.ItemId == staged.ItemId, cancellationToken).ConfigureAwait(false);
        if (current is not null) context.KnownSnapshots.Remove(current);
        context.KnownSnapshots.Add(new KnownSnapshotEntity { ItemId = staged.ItemId, SyncJobId = staged.SyncJobId, Path = staged.Path, Size = staged.Size, ModifiedUtc = staged.ModifiedUtc, ETag = staged.ETag, Hash = staged.Hash });
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> RemoveAsync(SyncJobId jobId, ItemId itemId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Unit>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            await RemoveBaselineAndRowsAsync(context, jobId, itemId, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static async Task RemoveBaselineAndRowsAsync(CloudSyncDbContext context, SyncJobId jobId, ItemId itemId, CancellationToken cancellationToken)
    {
        var current = await context.KnownSnapshots.SingleOrDefaultAsync(row => row.ItemId == itemId, cancellationToken).ConfigureAwait(false);
        var localOnly = new RemoteItemId(string.Empty);
        var path = current?.Path ?? new RelativePath(string.Empty);
        var rows = await context.Items
            .Where(row => row.Id == itemId || (current != null && row.SyncJobId == jobId && row.RemoteItemId == localOnly && row.LocalPath == path))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (current is not null) context.KnownSnapshots.Remove(current);
        context.Items.RemoveRange(rows);
    }

    private static StagedKnownEntity ToStaged(SyncJobId jobId, OperationId operationId, ItemId itemId, KnownSnapshot? target)
        => target is null
            ? new StagedKnownEntity { OperationId = operationId, SyncJobId = jobId, ItemId = itemId, Remove = true, Path = new RelativePath(string.Empty) }
            : new StagedKnownEntity { OperationId = operationId, SyncJobId = jobId, ItemId = itemId, Path = target.Path, Size = target.Size, ModifiedUtc = target.ModifiedUtc, ETag = target.ETag, Hash = target.Fingerprint?.Value };

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
