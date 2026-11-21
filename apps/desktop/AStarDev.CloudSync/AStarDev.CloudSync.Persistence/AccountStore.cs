using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IAccountStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class AccountStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IAccountStore
{
    private const int SqliteConstraint = 19;

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

    private static bool IsConstraintViolation(Exception exception)
        => exception is DbUpdateException { InnerException: SqliteException { SqliteErrorCode: SqliteConstraint } };

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);

    private static StoreError ToStoreError(Exception exception, AccountId id)
        => IsConstraintViolation(exception) ? new AccountAlreadyExists(id) : new StorageFailed(exception.Message);
}
