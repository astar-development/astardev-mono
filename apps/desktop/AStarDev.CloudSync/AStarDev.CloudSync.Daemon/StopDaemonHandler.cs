using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="StopDaemon"/> by recording the exit code and stopping the host shortly after the reply has gone out.</summary>
internal sealed class StopDaemonHandler(IHostApplicationLifetime lifetime, DaemonExit exit, TimeProvider timeProvider, ILogger<StopDaemonHandler> logger)
{
    /// <summary>Accepts the request and schedules a graceful stop after <see cref="ApplicationConstants.StopReplyGrace"/>, so the caller still receives the answer.</summary>
    /// <returns><see cref="CommandOutcome.Accepted"/> always. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<CommandResult, IpcError>> HandleAsync(StopDaemon command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        exit.Code = command.Restart ? ApplicationConstants.RestartExitCode : 0;
        DaemonLog.StopRequested(logger, command.Restart);
        _ = StopAfterReplyAsync(lifetime.ApplicationStopping);

        return Task.FromResult<Result<CommandResult, IpcError>>(new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)));
    }

    private async Task StopAfterReplyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(ApplicationConstants.StopReplyGrace, timeProvider, cancellationToken);
        lifetime.StopApplication();
    }
}
