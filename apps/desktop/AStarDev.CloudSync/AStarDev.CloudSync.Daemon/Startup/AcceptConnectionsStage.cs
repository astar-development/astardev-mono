using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 6 marker: <see cref="IpcHostedService"/> starts the IPC server right after the startup sequence, so UI connections are only accepted after recovery.</summary>
internal sealed class AcceptConnectionsStage : IStartupStage
{
    public string Name => ApplicationConstants.AcceptConnectionsStageName;

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
