namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 6: start the IPC server and accept UI connections, only after recovery. Real work arrives in the IPC slice (S3).</summary>
public sealed class AcceptConnectionsStage : IStartupStage
{
    public string Name => "Accept connections";

    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
