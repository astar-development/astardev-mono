using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="StopSync"/> by stopping the job's crawl through the <see cref="SyncRunner"/>.</summary>
internal sealed class StopSyncHandler(SyncRunner runner)
{
    /// <summary>Stops the crawl and answers once it has ended.</summary>
    /// <returns><see cref="CommandOutcome.Accepted"/> always, also when nothing was running. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(StopSync command, CancellationToken cancellationToken)
    {
        await runner.StopAsync(new SyncJobId(command.SyncJobId), cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }
}
