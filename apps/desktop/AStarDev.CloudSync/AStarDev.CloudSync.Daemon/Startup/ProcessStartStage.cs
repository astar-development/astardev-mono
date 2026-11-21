using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 1: host started, configuration loaded, logging initialised. The generic host has done this before the sequence runs.</summary>
internal sealed class ProcessStartStage : IStartupStage
{
    public string Name => ApplicationConstants.ProcessStartStageName;

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
