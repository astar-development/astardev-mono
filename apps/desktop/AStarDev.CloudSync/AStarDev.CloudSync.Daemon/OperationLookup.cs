using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Finds the provider and the item state behind operations, so a query can describe them to the user.</summary>
internal sealed class OperationLookup(ISyncJobStore jobs, IAccountStore accounts, CanonicalStateReader states)
{
    /// <summary>Maps each job to the provider type of its account. A job whose account is gone is left out.</summary>
    /// <returns>The provider of every known job, or the first store failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<IReadOnlyDictionary<SyncJobId, string>, StoreError>> ProvidersAsync(CancellationToken cancellationToken)
    {
        var allJobs = await jobs.ListAsync(cancellationToken);
        if (allJobs.Value is Fail<StoreError> jobsFailure) return jobsFailure;

        var allAccounts = await accounts.ListAsync(cancellationToken);
        if (allAccounts.Value is Fail<StoreError> accountsFailure) return accountsFailure;

        var typeOf = ((Ok<IReadOnlyList<Account>>)allAccounts.Value).Value.ToDictionary(account => account.Id, account => account.ProviderType);

        return new Ok<IReadOnlyDictionary<SyncJobId, string>>(((Ok<IReadOnlyList<SyncJob>>)allJobs.Value).Value.Where(job => typeOf.ContainsKey(job.AccountId)).ToDictionary(job => job.Id, job => typeOf[job.AccountId]));
    }

    /// <summary>Reads the state of the items the given operations act on, and no other item of their jobs.</summary>
    /// <param name="operations">The operations whose items to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The item states by item id, or the first store failure.</returns>
    public async Task<Result<IReadOnlyDictionary<ItemId, CanonicalItemState>, StoreError>> ItemsAsync(IReadOnlyList<Operation> operations, CancellationToken cancellationToken)
    {
        var byItem = new Dictionary<ItemId, CanonicalItemState>();
        foreach (var job in operations.GroupBy(operation => operation.SyncJobId))
        {
            var read = await states.ReadAsync(job.Key, job.Select(operation => operation.ItemId).ToHashSet(), cancellationToken);
            if (read.Value is Fail<StoreError> failure) return failure;

            foreach (var state in ((Ok<IReadOnlyList<CanonicalItemState>>)read.Value).Value) byItem[state.ItemId] = state;
        }

        return new Ok<IReadOnlyDictionary<ItemId, CanonicalItemState>>(byItem);
    }

    /// <summary>The path of an item: remote, else local, else last known; empty when the daemon does not know the item.</summary>
    /// <param name="state">The item state, or <see langword="null"/> when unknown.</param>
    public static string PathOf(CanonicalItemState? state) => state?.Remote?.Path.Value ?? state?.Local?.Path.Value ?? state?.Known?.Path.Value ?? string.Empty;

    /// <summary>The size of an item: remote, else local, else last known; zero when the daemon does not know the item.</summary>
    /// <param name="state">The item state, or <see langword="null"/> when unknown.</param>
    public static long SizeOf(CanonicalItemState? state) => state?.Remote?.Size ?? state?.Local?.Size ?? state?.Known?.Size ?? 0;
}
