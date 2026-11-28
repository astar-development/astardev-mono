using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IPlanStore"/>. One context and one transaction per batch, so a batch costs one commit and a failure or crash leaves none of it written.</summary>
internal sealed class PlanStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IPlanStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ConflictId>, StoreError>> SaveAsync(PlanBatch batch, CancellationToken cancellationToken)
    {
        if (batch.IsEmpty) return new Ok<IReadOnlyList<ConflictId>>([]);

        var outcome = await Try.RunAsync(token => WriteAsync(batch, token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<IReadOnlyList<ConflictId>, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    private async Task<Result<IReadOnlyList<ConflictId>, StoreError>> WriteAsync(PlanBatch batch, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (NeedsJob(batch) && !await context.SyncJobs.AnyAsync(job => job.Id == batch.JobId, cancellationToken).ConfigureAwait(false)) return new Fail<StoreError>(new SyncJobNotFound(batch.JobId));

        foreach (var item in batch.Forgotten) await KnownSnapshotStore.RemoveBaselineAndRowsAsync(context, batch.JobId, item, cancellationToken).ConfigureAwait(false);
        await StageAndQueueAsync(context, batch, cancellationToken).ConfigureAwait(false);
        var stored = await AddConflictsAsync(context, batch.Conflicts, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new Ok<IReadOnlyList<ConflictId>>(stored);
    }

    private static bool NeedsJob(PlanBatch batch) => batch.Work.Count > 0 || batch.Conflicts.Count > 0;

    private static async Task StageAndQueueAsync(CloudSyncDbContext context, PlanBatch batch, CancellationToken cancellationToken)
    {
        if (batch.Work.Count == 0) return;

        var staged = batch.Work.Select(work => KnownSnapshotStore.ToStaged(batch.JobId, work.Operations[^1].Id, work.Operations[^1].ItemId, work.Target)).ToList();
        var operationIds = staged.Select(row => row.OperationId).ToList();
        await context.StagedKnownSnapshots.Where(row => operationIds.Contains(row.OperationId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        context.StagedKnownSnapshots.AddRange(staged);
        context.Operations.AddRange(batch.Work.SelectMany(work => work.Operations).Select(OperationEntity.From));
    }

    private static async Task<IReadOnlyList<ConflictId>> AddConflictsAsync(CloudSyncDbContext context, IReadOnlyList<PlannedConflict> conflicts, CancellationToken cancellationToken)
    {
        if (conflicts.Count == 0) return [];

        var items = conflicts.Select(planned => planned.Conflict.ItemId).Distinct().ToList();
        var waiting = await context.Conflicts.AsNoTracking()
            .Where(row => items.Contains(row.ItemId) && (row.Status == ConflictStatus.Open || row.Status == ConflictStatus.Deferred))
            .Select(row => new { row.ItemId, row.Type })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var taken = waiting.Select(row => (row.ItemId, row.Type)).ToHashSet();
        var stored = conflicts.Where(planned => taken.Add((planned.Conflict.ItemId, planned.Conflict.Type))).ToList();
        context.Conflicts.AddRange(stored.Select(planned => ConflictEntity.From(planned.Conflict)));
        context.EventJournal.AddRange(stored.Select(planned => JournalEntryEntity.From(planned.Detected)));

        return [.. stored.Select(planned => planned.Conflict.Id)];
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
