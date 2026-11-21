using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ISyncScopeStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class SyncScopeStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ISyncScopeStore
{
    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> AddAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<Unit, StoreError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            if (!await context.SyncJobs.AnyAsync(job => job.Id == scope.SyncJobId, token).ConfigureAwait(false)) return new Fail<StoreError>(new SyncJobNotFound(scope.SyncJobId));

            context.SyncScopes.Add(SyncScopeEntity.From(scope));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => ToStoreError(exception, scope)) switch
        {
            Ok<Result<Unit, StoreError>> ok => ok.Value,
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> RemoveAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entity = await context.SyncScopes.SingleOrDefaultAsync(row => row.SyncJobId == scope.SyncJobId && row.RemoteRootId == scope.RemoteRootId, token).ConfigureAwait(false);
            if (entity is null) return false;

            context.SyncScopes.Remove(entity);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<bool> { Value: true } => new Ok<Unit>(default),
            Ok<bool> => new Fail<StoreError>(new SyncScopeNotFound(scope)),
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<SyncScope>, StoreError>> ListAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entities = await context.SyncScopes.AsNoTracking().Where(entity => entity.SyncJobId == syncJobId).OrderBy(entity => entity.RemoteRootId).ToListAsync(token).ConfigureAwait(false);

            return (IReadOnlyList<SyncScope>)[.. entities.Select(entity => entity.ToScope())];
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);

    private static StoreError ToStoreError(Exception exception, SyncScope scope)
        => SqliteErrors.IsConstraintViolation(exception) ? new SyncScopeAlreadyExists(scope) : new StorageFailed(exception.Message);
}
