using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IConflictStore"/>. Each call uses its own short-lived context.</summary>
internal sealed class ConflictStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IConflictStore
{
    /// <inheritdoc/>
    public async Task<Result<bool, StoreError>> AddAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<bool, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncJobs.AnyAsync(job => job.Id == conflict.SyncJobId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncJobNotFound(conflict.SyncJobId));

            var type = conflict.Type;
            var open = await context.Conflicts.AnyAsync(row => row.ItemId == conflict.ItemId && row.Type == type && (row.Status == ConflictStatus.Open || row.Status == ConflictStatus.Deferred), token).ConfigureAwait(false);
            if (open) return new Ok<bool>(false);

            context.Conflicts.Add(ConflictEntity.From(conflict));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<bool>(true);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<bool, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<Conflict>, StoreError>> ListOpenAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<Conflict>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.Conflicts.AsNoTracking().Where(row => row.SyncJobId == jobId && (row.Status == ConflictStatus.Open || row.Status == ConflictStatus.Deferred)).ToListAsync(token).ConfigureAwait(false);

            return rows.OrderBy(row => row.CreatedUtc).Select(row => row.ToConflict()).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Conflict, StoreError>> GetAsync(ConflictId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Conflict, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var row = await context.Conflicts.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, token).ConfigureAwait(false);

            return row is null ? new Fail<StoreError>(new ConflictNotFound(id)) : new Ok<Conflict>(row.ToConflict());
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Conflict, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> UpdateStatusAsync(ConflictId id, ConflictStatus status, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var row = await context.Conflicts.FirstOrDefaultAsync(entity => entity.Id == id, token).ConfigureAwait(false);
            if (row is null) return new Fail<StoreError>(new ConflictNotFound(id));

            row.Status = status;
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
    public async Task<Result<int, StoreError>> CountOpenAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.Conflicts.AsNoTracking().CountAsync(row => row.Status == ConflictStatus.Open || row.Status == ConflictStatus.Deferred, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
