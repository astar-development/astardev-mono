using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal sealed class FakeAccountStore(params Account[] accounts) : IAccountStore
{
    public List<Account> Accounts { get; } = [.. accounts];

    public Result<Unit, StoreError>? UpdateResult { get; init; }

    public Task<Result<Unit, StoreError>> AddAsync(Account account, CancellationToken cancellationToken)
    {
        Accounts.Add(account);

        return Task.FromResult<Result<Unit, StoreError>>(new Ok<Unit>(default));
    }

    public Task<Result<Account, StoreError>> GetAsync(AccountId id, CancellationToken cancellationToken)
        => Task.FromResult<Result<Account, StoreError>>(new Ok<Account>(Accounts.Single(account => account.Id == id)));

    public Task<Result<IReadOnlyList<Account>, StoreError>> ListAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<IReadOnlyList<Account>, StoreError>>(new Ok<IReadOnlyList<Account>>(Accounts.ToList()));

    public Task<Result<Unit, StoreError>> UpdateStatusAsync(AccountId id, AccountStatus status, CancellationToken cancellationToken)
    {
        if (UpdateResult is { } forced) return Task.FromResult(forced);
        var index = Accounts.FindIndex(account => account.Id == id);
        Accounts[index] = Accounts[index] with { Status = status };

        return Task.FromResult<Result<Unit, StoreError>>(new Ok<Unit>(default));
    }

    public Task<Result<Unit, StoreError>> RemoveAsync(AccountId id, CancellationToken cancellationToken)
    {
        Accounts.RemoveAll(account => account.Id == id);

        return Task.FromResult<Result<Unit, StoreError>>(new Ok<Unit>(default));
    }
}
