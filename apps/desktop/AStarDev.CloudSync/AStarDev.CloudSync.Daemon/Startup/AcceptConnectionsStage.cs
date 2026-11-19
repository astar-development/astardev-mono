using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 6: start the IPC server and accept UI connections, only after recovery. Real work arrives in the IPC slice (S3).</summary>
public sealed class AcceptConnectionsStage : IStartupStage
{
    public string Name => "Accept connections";

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
