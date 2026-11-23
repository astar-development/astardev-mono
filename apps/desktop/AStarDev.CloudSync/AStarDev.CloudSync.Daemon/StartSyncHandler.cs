using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="StartSync"/> by asking the <see cref="SyncRunner"/> to crawl the job in the background.</summary>
internal sealed class StartSyncHandler(SyncRunner runner, ILogger<StartSyncHandler> logger)
{
    /// <summary>Starts the crawl and answers without waiting for it.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once the crawl runs, also when it already ran, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the job is unknown or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(StartSync command, CancellationToken cancellationToken)
        => await runner.StartAsync(new SyncJobId(command.SyncJobId), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "read", "sync job"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.StartSyncFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
