using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="AddSyncScope"/> by storing the folder as a scope of the job in the <see cref="ISyncScopeStore"/>, then starting the job's crawl and local observation, or waking the crawl when it already runs, so the folder syncs at once.</summary>
internal sealed class AddSyncScopeHandler(ISyncScopeStore store, IJobAccounts jobAccounts, ICloudProvider provider, SyncRunner runner, LocalObservationRunner observation, AuditJournal audit, ILogger<AddSyncScopeHandler> logger)
{
    /// <summary>Stores the folder as a scope of the job.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored, recorded in the audit trail and handed to the running crawl, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the folder cannot be found in the drive, the job is unknown, already holds the folder or the store fails. The folder's path from the drive root is looked up first and stored with the scope, because it decides which local paths the scope owns.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(AddSyncScope command, CancellationToken cancellationToken)
        => await jobAccounts.AccountIdentifierAsync(new SyncJobId(command.SyncJobId), cancellationToken) switch
        {
            Ok<string> account => await FindPathAsync(account.Value, command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "read", "sync job"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> FindPathAsync(string account, AddSyncScope command, CancellationToken cancellationToken)
        => await RemoteFolderPaths.FindAsync(provider, account, new RemoteItemId(command.RemoteRootId), cancellationToken) switch
        {
            Ok<RelativePath> path => await StoreAsync(command, path.Value, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> StoreAsync(AddSyncScope command, RelativePath path, CancellationToken cancellationToken)
        => await store.AddAsync(new SyncScope(new SyncJobId(command.SyncJobId), new RemoteItemId(command.RemoteRootId), path), cancellationToken) switch
        {
            Ok<Unit> => await StartAsync(command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved", "folder selection"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> StartAsync(AddSyncScope command, CancellationToken cancellationToken)
    {
        var jobId = new SyncJobId(command.SyncJobId);
        var started = await runner.StartAsync(jobId, cancellationToken);
        if (started.Value is Fail<StoreError> crawlFail) return Reject(StoreErrorMessages.Describe(crawlFail.Error, "read", "sync job"), ErrorInfo.CodeOf(crawlFail.Error.Value));

        var observing = await observation.StartAsync(jobId, cancellationToken);
        if (observing.Value is Fail<StoreError> observationFail) return Reject(StoreErrorMessages.Describe(observationFail.Error, "read", "sync job"), ErrorInfo.CodeOf(observationFail.Error.Value));

        return await RecordAsync(command, cancellationToken);
    }

    private async Task<Result<CommandResult, IpcError>> RecordAsync(AddSyncScope command, CancellationToken cancellationToken)
    {
        await audit.RecordAsync("SyncScopeAdded", command.SyncJobId, new { syncJobId = command.SyncJobId, remoteRootId = command.RemoteRootId }, cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.AddSyncScopeFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
