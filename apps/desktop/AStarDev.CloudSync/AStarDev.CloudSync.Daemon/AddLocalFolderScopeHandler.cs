using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="AddLocalFolderScope"/> by creating the remote folder of the same name at the top of the job account's drive, then adding it as a scope through <see cref="AddSyncScopeHandler"/>.</summary>
internal sealed class AddLocalFolderScopeHandler(IJobAccounts jobAccounts, ICloudProvider provider, AddSyncScopeHandler addSyncScope, ILogger<AddLocalFolderScopeHandler> logger)
{
    private const string NotOneFolderMessage = "Only a single folder directly inside the local folder can be added.";

    /// <summary>Creates the remote folder, reusing one already at that path, and stores it as a scope of the job.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored and handed to the running crawl, <see cref="CommandOutcome.ValidationError"/> when the name is not one folder, so the drive is not touched,
    /// or <see cref="CommandOutcome.Rejected"/> with a message for the user when the job is unknown, the remote folder cannot be created or the scope cannot be added. The drive-relative path is the folder's name, because the job's local root mirrors the drive root.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(AddLocalFolderScope command, CancellationToken cancellationToken)
        => IsOneFolder(command.FolderName)
            ? await ForAccountAsync(command, cancellationToken)
            : new Ok<CommandResult>(new CommandResult(CommandOutcome.ValidationError, NotOneFolderMessage));

    private async Task<Result<CommandResult, IpcError>> ForAccountAsync(AddLocalFolderScope command, CancellationToken cancellationToken)
        => await jobAccounts.AccountIdentifierAsync(new SyncJobId(command.SyncJobId), cancellationToken) switch
        {
            Ok<string> account => await CreateAsync(account.Value, command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "read", "sync job"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> CreateAsync(string account, AddLocalFolderScope command, CancellationToken cancellationToken)
    {
        var path = new RelativePath(command.FolderName);

        return await provider.CreateFolderAsync(account, path, cancellationToken) switch
        {
            Ok<RemoteItemId> folder => await addSyncScope.AddAtPathAsync(new AddSyncScope(command.SyncJobId, folder.Value.Value), path, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error), ErrorInfo.CodeOf(fail.Error.Value))
        };
    }

    private static bool IsOneFolder(string name)
        => !string.IsNullOrWhiteSpace(name) && name is not ("." or "..") && name.IndexOfAny(['/', '\\']) < 0;

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.AddLocalFolderScopeFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
