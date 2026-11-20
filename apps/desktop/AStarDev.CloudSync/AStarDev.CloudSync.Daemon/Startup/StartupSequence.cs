using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

public sealed class StartupSequence(IEnumerable<IStartupStage> stages, ILogger<StartupSequence> logger)
{
    private readonly IReadOnlyList<IStartupStage> _stages = [.. stages];

    public async Task<Result<Unit, StartupError>> RunAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < _stages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stage = _stages[index];
            logger.LogInformation("Startup stage {Number}/{Total} starting: {Stage}", index + 1, _stages.Count, stage.Name);

            var result = await stage.ExecuteAsync(cancellationToken);
            if (result is Fail<StartupError> fail)
            {
                logger.LogCritical(fail.Error.Exception, "Startup stage {Number}/{Total} failed: {Stage}: {Cause}. Stopping.", index + 1, _stages.Count, stage.Name, fail.Error.Cause);
                return result;
            }

            logger.LogInformation("Startup stage {Number}/{Total} complete: {Stage}", index + 1, _stages.Count, stage.Name);
        }

        return new Ok<Unit>(default);
    }
}
