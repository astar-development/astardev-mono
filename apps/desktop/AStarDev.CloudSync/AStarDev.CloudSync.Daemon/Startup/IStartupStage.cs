namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>One stage of the doc 14 startup sequence. Stages run in registration order.</summary>
public interface IStartupStage
{
    string Name { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
