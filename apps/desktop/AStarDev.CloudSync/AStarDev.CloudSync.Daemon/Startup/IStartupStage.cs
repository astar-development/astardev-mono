using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>One stage of the doc 14 startup sequence. Stages run in registration order.</summary>
public interface IStartupStage
{
    string Name { get; }

    /// <summary>Expected failures come back as a <see cref="StartupError"/>; cancellation propagates as <see cref="OperationCanceledException"/>.</summary>
    Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken);
}
