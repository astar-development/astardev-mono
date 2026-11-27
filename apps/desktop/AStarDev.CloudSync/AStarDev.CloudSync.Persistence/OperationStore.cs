using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IOperationStore"/>. Each call uses its own short-lived context. A claim is one SQL statement, so SQLite serialises concurrent claims and the per-item lock cannot be raced.</summary>
internal sealed class OperationStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IOperationStore
{
    private static readonly DateTimeOffsetToBinaryConverter TimeConverter = new();

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> EnqueueAsync(Operation operation, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncJobs.AnyAsync(job => job.Id == operation.SyncJobId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncJobNotFound(operation.SyncJobId));

            context.Operations.Add(OperationEntity.From(operation));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return Flatten(outcome);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<Operation>, StoreError>> ClaimNextAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<Operation>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var due = ToStored(nowUtc);
            var claimed = new List<Operation>();
            while (claimed.Count < limit)
            {
                var rows = await context.Operations.FromSql($"""
                    UPDATE Operations SET Status = 'Running', Attempts = Attempts + 1
                    WHERE Id = (
                        SELECT candidate.Id FROM Operations AS candidate
                        WHERE candidate.Status = 'Pending' AND candidate.NextAttemptUtc <= {due}
                          AND NOT EXISTS (SELECT 1 FROM Operations AS running WHERE running.ItemId = candidate.ItemId AND running.Status = 'Running')
                        ORDER BY candidate.Priority, candidate.CreatedUtc
                        LIMIT 1)
                    RETURNING *
                    """).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                if (rows.Count == 0) break;

                claimed.Add(rows[0].ToOperation());
            }

            return claimed;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> CompleteAsync(OperationId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var changed = await context.Operations.Where(row => row.Id == id && row.Status == OperationStatus.Running)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OperationStatus.Completed), token).ConfigureAwait(false);

            return changed == 0 ? new Fail<StoreError>(new OperationNotRunning(id)) : new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return Flatten(outcome);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> FailAsync(OperationId id, string error, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var running = context.Operations.Where(row => row.Id == id && row.Status == OperationStatus.Running);
            var changed = retryAtUtc is { } retryAt
                ? await running.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OperationStatus.Pending).SetProperty(row => row.LastError, error).SetProperty(row => row.NextAttemptUtc, retryAt.ToUniversalTime()), token).ConfigureAwait(false)
                : await running.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OperationStatus.DeadLetter).SetProperty(row => row.LastError, error), token).ConfigureAwait(false);

            return changed == 0 ? new Fail<StoreError>(new OperationNotRunning(id)) : new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return Flatten(outcome);
    }

    /// <inheritdoc/>
    public async Task<Result<Page<Operation>, StoreError>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Page<Operation>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var deadLetters = context.Operations.AsNoTracking().Where(row => row.Status == OperationStatus.DeadLetter);
            var total = await deadLetters.CountAsync(token).ConfigureAwait(false);
            var rows = await deadLetters.OrderBy(row => row.Priority).ThenBy(row => row.CreatedUtc).ThenBy(row => row.Id).Skip(skip).Take(take).ToListAsync(token).ConfigureAwait(false);

            return new Page<Operation>(total, [.. rows.Select(row => row.ToOperation())]);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> RequeueDeadLetterAsync(OperationId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var changed = await context.Operations.Where(row => row.Id == id && row.Status == OperationStatus.DeadLetter)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OperationStatus.Pending).SetProperty(row => row.Attempts, 0), token).ConfigureAwait(false);

            return changed == 0 ? new Fail<StoreError>(new OperationNotDeadLettered(id)) : new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return Flatten(outcome);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> DiscardDeadLetterAsync(OperationId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var changed = await context.Operations.Where(row => row.Id == id && row.Status == OperationStatus.DeadLetter).ExecuteDeleteAsync(token).ConfigureAwait(false);

            return changed == 0 ? new Fail<StoreError>(new OperationNotDeadLettered(id)) : new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return Flatten(outcome);
    }

    /// <inheritdoc/>
    public async Task<Result<int, StoreError>> RequeueRunningAsync(string reason, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.Operations.Where(row => row.Status == OperationStatus.Running)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OperationStatus.Pending).SetProperty(row => row.LastError, reason), token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<OperationStatus, int>, StoreError>> CountByStatusAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyDictionary<OperationStatus, int>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.Operations.AsNoTracking().GroupBy(row => row.Status).Select(group => new { Status = group.Key, Count = group.Count() }).ToListAsync(token).ConfigureAwait(false);

            return rows.ToDictionary(row => row.Status, row => row.Count);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<Operation>, StoreError>> ListAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<Operation>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.Operations.AsNoTracking().OrderBy(row => row.Priority).ThenBy(row => row.CreatedUtc).ToListAsync(token).ConfigureAwait(false);

            return rows.Select(row => row.ToOperation()).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static long ToStored(DateTimeOffset value) => (long)TimeConverter.ConvertToProvider(value.ToUniversalTime())!;

    private static Result<Unit, StoreError> Flatten(Exceptional<Result<Unit, StoreError>> outcome)
        => outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
