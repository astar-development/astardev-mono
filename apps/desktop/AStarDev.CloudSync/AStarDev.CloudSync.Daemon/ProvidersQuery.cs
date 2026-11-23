using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetProviders"/> from the registered <see cref="ICloudProvider"/> and the account, job, operation and provider state stores.</summary>
internal sealed class ProvidersQuery(ICloudProvider provider, IAccountStore accounts, ISyncJobStore jobs, IOperationStore operations, IProviderStateStore states, ILogger<ProvidersQuery> logger)
{
    /// <summary>
    /// Lists the registered provider and one set of figures per account. The provider holds one sign-in, so its quota is shown for every account that does not need to sign in again.
    /// </summary>
    /// <returns>The providers and the figures, or <see cref="HandlerFailed"/> carrying the first store failure worded for the user. A quota that cannot be read is logged and leaves the storage figures empty. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ProvidersView, IpcError>> HandleAsync(GetProviders query, CancellationToken cancellationToken)
    {
        var accountList = await accounts.ListAsync(cancellationToken);
        if (accountList.Value is Fail<StoreError> accountsFailure) return Failure(accountsFailure.Error, "accounts");

        var jobList = await jobs.ListAsync(cancellationToken);
        if (jobList.Value is Fail<StoreError> jobsFailure) return Failure(jobsFailure.Error, "sync jobs");

        var queue = await operations.ListAsync(cancellationToken);
        if (queue.Value is Fail<StoreError> queueFailure) return Failure(queueFailure.Error, "operation queue");

        var synced = await states.ListLastSyncAsync(cancellationToken);
        if (synced.Value is Fail<StoreError> syncedFailure) return Failure(syncedFailure.Error, "last syncs");

        var known = ((Ok<IReadOnlyList<Account>>)accountList.Value).Value;
        var drive = await DriveAsync(known, cancellationToken);
        var jobsByAccount = ((Ok<IReadOnlyList<SyncJob>>)jobList.Value).Value.ToLookup(job => job.AccountId);
        var operationList = ((Ok<IReadOnlyList<Operation>>)queue.Value).Value;
        var lastSyncs = ((Ok<IReadOnlyDictionary<SyncJobId, DateTimeOffset>>)synced.Value).Value;

        return new Ok<ProvidersView>(new ProvidersView(
            [new ProviderView(ApplicationConstants.OneDriveProviderType, [.. provider.Features.SelectMany(CapabilitiesOf)])],
            [.. known.Select(account => InsightsOf(account, jobsByAccount[account.Id].Select(job => job.Id).ToHashSet(), operationList, lastSyncs, drive))]));
    }

    private async Task<Option<ProviderDriveInfo>> DriveAsync(IReadOnlyList<Account> known, CancellationToken cancellationToken)
    {
        if (known.All(account => account.Status == AccountStatus.ReauthenticationRequired)) return Option.None<ProviderDriveInfo>();

        var info = await provider.GetDriveInfoAsync(cancellationToken);
        if (info.Value is Fail<ProviderError> failure) DaemonLog.DriveInfoUnavailable(logger, ProviderErrorMessages.Describe(failure.Error), ErrorInfo.CodeOf(failure.Error.Value));

        return info.Value is Ok<ProviderDriveInfo> ok ? Option.Some(ok.Value) : Option.None<ProviderDriveInfo>();
    }

    private static ProviderInsightsView InsightsOf(Account account, HashSet<SyncJobId> accountJobs, IReadOnlyList<Operation> operationList, IReadOnlyDictionary<SyncJobId, DateTimeOffset> lastSyncs, Option<ProviderDriveInfo> drive)
    {
        var own = operationList.Where(operation => accountJobs.Contains(operation.SyncJobId)).ToList();
        var synced = accountJobs.Where(lastSyncs.ContainsKey).Select(job => (DateTimeOffset?)lastSyncs[job]).Max();
        var quota = account.Status == AccountStatus.ReauthenticationRequired ? Option.None<ProviderDriveInfo>() : drive;

        return new ProviderInsightsView(
            account.Id.Value,
            synced,
            quota.Value is Some<ProviderDriveInfo> used ? used.Value.UsedBytes : null,
            quota.Value is Some<ProviderDriveInfo> total ? total.Value.TotalBytes : null,
            own.Count(operation => operation.Status == OperationStatus.Pending),
            own.Count(operation => operation.Status == OperationStatus.Running),
            own.Count(operation => operation.Status is OperationStatus.Failed or OperationStatus.DeadLetter));
    }

    private static IEnumerable<ProviderCapability> CapabilitiesOf(ProviderFeature feature) => Enum.TryParse<ProviderCapability>(feature.ToString(), out var capability) ? [capability] : [];

    private static Fail<IpcError> Failure(StoreError error, string subject) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", subject)));
}
