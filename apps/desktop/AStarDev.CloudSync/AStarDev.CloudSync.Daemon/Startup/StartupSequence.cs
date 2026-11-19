using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

public sealed class StartupSequence(IEnumerable<IStartupStage> stages, ILogger<StartupSequence> logger)
{
    private readonly IReadOnlyList<IStartupStage> _stages = [.. stages];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < _stages.Count; index++)
        {
            var stage = _stages[index];
            logger.LogInformation("Startup stage {Number}/{Total} starting: {Stage}", index + 1, _stages.Count, stage.Name);

            try
            {
                await stage.RunAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogCritical(ex, "Startup stage {Number}/{Total} failed: {Stage}. Stopping.", index + 1, _stages.Count, stage.Name);
                throw new StartupFailedException(stage.Name, ex);
            }

            logger.LogInformation("Startup stage {Number}/{Total} complete: {Stage}", index + 1, _stages.Count, stage.Name);
        }
    }
}
