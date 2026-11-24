using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="StartReclassify"/> by starting a run in the background and answering without waiting for it.</summary>
/// <param name="reclassifier">Runs the reclassify.</param>
/// <param name="tracker">Runs it one at a time and publishes its progress.</param>
/// <param name="lifetime">Stops a run that is still going when the daemon shuts down.</param>
internal sealed class StartReclassifyHandler(IClassificationReclassifier reclassifier, ReclassifyTracker tracker, IHostApplicationLifetime lifetime)
{
    private const string AlreadyRunning = "A reclassify is already running.";

    /// <summary>Starts the run.</summary>
    /// <returns><see cref="CommandOutcome.Accepted"/> once the run goes on in the background, or <see cref="CommandOutcome.Rejected"/> with a message for the user when a run is already active.</returns>
    public Task<Result<CommandResult, IpcError>> HandleAsync(StartReclassify command, CancellationToken cancellationToken)
    {
        if (tracker.Running) return Task.FromResult<Result<CommandResult, IpcError>>(new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, AlreadyRunning)));

        _ = tracker.RunAsync(progress => reclassifier.StartAsync(progress, lifetime.ApplicationStopping), lifetime.ApplicationStopping);

        return Task.FromResult<Result<CommandResult, IpcError>>(new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)));
    }
}
