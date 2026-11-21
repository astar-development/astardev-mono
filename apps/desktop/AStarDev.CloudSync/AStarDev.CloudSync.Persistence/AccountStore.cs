using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IAccountStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class AccountStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IAccountStore
{
    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> AddAsync(Account account, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            context.Accounts.Add(AccountEntity.From(account));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default(Unit);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => ToStoreError(exception, account.Id));
    }

    /// <inheritdoc/>
    public async Task<Result<Account, StoreError>> GetAsync(AccountId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.Accounts.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure) switch
        {
            Ok<AccountEntity> { Value: { } entity } => new Ok<Account>(entity.ToAccount()),
            Ok<AccountEntity> => new Fail<StoreError>(new AccountNotFound(id)),
            Fail<StoreError> fail => fail
        };
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<Account>, StoreError>> ListAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entities = await context.Accounts.AsNoTracking().OrderBy(entity => entity.CreatedUtc).ToListAsync(token).ConfigureAwait(false);

            return (IReadOnlyList<Account>)[.. entities.Select(entity => entity.ToAccount())];
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> UpdateStatusAsync(AccountId id, AccountStatus status, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entity = await context.Accounts.SingleOrDefaultAsync(account => account.Id == id, token).ConfigureAwait(false);
            if (entity is null) return false;

            entity.Status = status;
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ToUnitResult(outcome, id);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> RemoveAsync(AccountId id, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entity = await context.Accounts.SingleOrDefaultAsync(account => account.Id == id, token).ConfigureAwait(false);
            if (entity is null) return false;

            context.Accounts.Remove(entity);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ToUnitResult(outcome, id);
    }

    private static Result<Unit, StoreError> ToUnitResult(Exceptional<bool> outcome, AccountId id)
        => outcome.ToResult(ToStorageFailure) switch
        {
            Ok<bool> { Value: true } => new Ok<Unit>(default),
            Ok<bool> => new Fail<StoreError>(new AccountNotFound(id)),
            Fail<StoreError> fail => fail
        };

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);

    private static StoreError ToStoreError(Exception exception, AccountId id)
        => SqliteErrors.IsConstraintViolation(exception) ? new AccountAlreadyExists(id) : new StorageFailed(exception.Message);
}
