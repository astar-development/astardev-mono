using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ILocalObservationStore"/>. A local-only item gets an empty remote id, so a later remote crawl never matches it by id.</summary>
internal sealed class LocalObservationStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ILocalObservationStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<LocalSnapshot>, StoreError>> ListPresentAsync(SyncJobId jobId, RelativePath subtree, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<LocalSnapshot>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var items = ItemsUnder(context, jobId, subtree);
            var rows = await (from snapshot in context.ItemSnapshots.AsNoTracking()
                              join item in items.AsNoTracking() on snapshot.ItemId equals item.Id
                              where snapshot.SnapshotSource == SnapshotSource.Local
                              select new { item.LocalPath, Snapshot = snapshot }).ToListAsync(token).ConfigureAwait(false);

            return rows.GroupBy(row => row.LocalPath)
                .Select(group => group.MaxBy(row => row.Snapshot.ObservedUtc)!)
                .Where(row => row.Snapshot.Size != ILocalObservationStore.DeletedSize)
                .Select(row => new LocalSnapshot(row.LocalPath, row.Snapshot.Size, row.Snapshot.ModifiedUtc, row.Snapshot.LocalIdentity is { } identity ? new LocalIdentity(identity) : null, row.Snapshot.Hash is { } hash ? new ContentFingerprint(hash) : null))
                .ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> RecordAsync(SyncJobId jobId, IReadOnlyList<LocalSnapshot> present, IReadOnlyList<RelativePath> deleted, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncJobs.AnyAsync(row => row.Id == jobId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncJobNotFound(jobId));

            var paths = present.Select(snapshot => snapshot.Path).Concat(deleted).Distinct().ToList();
            var known = (await context.Items.Where(row => row.SyncJobId == jobId && paths.Contains(row.LocalPath)).ToListAsync(token).ConfigureAwait(false))
                .OrderBy(row => row.CreatedUtc)
                .GroupBy(row => row.LocalPath)
                .ToDictionary(group => group.Key, group => group.First());
            var latest = await ItemSnapshotRows.LoadAsync(context, [.. known.Values.Select(row => row.Id)], SnapshotSource.Local, token).ConfigureAwait(false);

            foreach (var snapshot in present) AddPresent(context, jobId, known, latest, snapshot, observedUtc);
            foreach (var path in deleted) AddDeleted(context, known, latest, path, observedUtc);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <summary>The items of <paramref name="jobId"/> at <paramref name="subtree"/> or below it; an empty subtree means every item of the job. A sibling that only shares the name prefix (<c>docs2</c> for <c>docs</c>) is not under it. Under SQLite BINARY collation <c>0</c> follows <c>/</c>, so <c>[subtree/, subtree0)</c> holds exactly the paths starting <c>subtree/</c>; bounding the whole match by <c>[subtree, subtree0)</c> lets it seek the <c>(SyncJobId, LocalPath)</c> index as one range.</summary>
    internal static IQueryable<ItemEntity> ItemsUnder(CloudSyncDbContext context, SyncJobId jobId, RelativePath subtree)
    {
        if (string.IsNullOrEmpty(subtree.Value)) return context.Items.Where(row => row.SyncJobId == jobId);

        var path = subtree.Value;
        var prefix = path + "/";
        var pastPrefix = path + "0";

        return context.Items.FromSql($"SELECT * FROM Items WHERE SyncJobId = {jobId.Value} AND LocalPath >= {path} AND LocalPath < {pastPrefix} AND (LocalPath = {path} OR LocalPath >= {prefix})");
    }

    private static void AddPresent(CloudSyncDbContext context, SyncJobId jobId, Dictionary<RelativePath, ItemEntity> known, Dictionary<ItemId, ItemSnapshotEntity> latest, LocalSnapshot snapshot, DateTimeOffset observedUtc)
    {
        if (!known.TryGetValue(snapshot.Path, out var item))
        {
            item = new ItemEntity { Id = new ItemId(Guid.NewGuid()), SyncJobId = jobId, ItemType = ItemType.File, RemoteItemId = new RemoteItemId(string.Empty), LocalPath = snapshot.Path, CreatedUtc = observedUtc, UpdatedUtc = observedUtc };
            known[snapshot.Path] = item;
            context.Items.Add(item);
        }
        else item.UpdatedUtc = observedUtc;

        ItemSnapshotRows.Observe(context, latest, item.Id, SnapshotSource.Local, snapshot.Size, snapshot.ModifiedUtc, snapshot.Fingerprint?.Value, snapshot.Identity?.Value, observedUtc);
    }

    private static void AddDeleted(CloudSyncDbContext context, Dictionary<RelativePath, ItemEntity> known, Dictionary<ItemId, ItemSnapshotEntity> latest, RelativePath path, DateTimeOffset observedUtc)
    {
        if (!known.TryGetValue(path, out var item) || !latest.TryGetValue(item.Id, out var last) || last.Size == ILocalObservationStore.DeletedSize) return;

        item.UpdatedUtc = observedUtc;
        ItemSnapshotRows.Observe(context, latest, item.Id, SnapshotSource.Local, ILocalObservationStore.DeletedSize, last.ModifiedUtc, null, last.LocalIdentity, observedUtc);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
