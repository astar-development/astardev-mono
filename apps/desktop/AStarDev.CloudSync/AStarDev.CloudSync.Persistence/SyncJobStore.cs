using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ISyncJobStore"/>. Each call uses its own short-lived context; adding is serialised so two requests cannot both create the job of one account.</summary>
internal sealed class SyncJobStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ISyncJobStore
{
    private readonly SemaphoreSlim addGate = new(1, 1);

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> AddAsync(SyncJob job, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await addGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await InsertAsync(job, token).ConfigureAwait(false);
            }
            finally
            {
                addGate.Release();
            }
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<SyncJob, StoreError>> GetAsync(SyncJobId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.SyncJobs.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<SyncJobEntity> { Value: { } entity } => new Ok<SyncJob>(entity.ToJob()),
            Ok<SyncJobEntity> => new Fail<StoreError>(new SyncJobNotFound(id)),
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<SyncJob>, StoreError>> ListAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entities = await context.SyncJobs.AsNoTracking().OrderBy(entity => entity.CreatedUtc).ToListAsync(token).ConfigureAwait(false);

            return (IReadOnlyList<SyncJob>)[.. entities.Select(entity => entity.ToJob())];
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private async Task<Result<Unit, StoreError>> InsertAsync(SyncJob job, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await context.Accounts.AnyAsync(account => account.Id == job.AccountId, cancellationToken).ConfigureAwait(false)) return new Fail<StoreError>(new AccountNotFound(job.AccountId));

        var existing = await context.SyncJobs.AsNoTracking().Where(entity => entity.AccountId == job.AccountId).Select(entity => entity.Id).Cast<SyncJobId?>().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is { } existingId) return new Fail<StoreError>(new SyncJobAlreadyExists(existingId));

        context.SyncJobs.Add(SyncJobEntity.From(job));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new Ok<Unit>(default);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
