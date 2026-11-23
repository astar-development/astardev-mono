using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Runs the registered <see cref="IStartupStage"/> instances one at a time and logs each stage's start, completion or failure.</summary>
internal sealed class StartupSequence(IEnumerable<IStartupStage> stages, ILogger<StartupSequence> logger)
{
    private readonly IReadOnlyList<IStartupStage> stages = [.. stages];

    /// <summary>Runs the stages in registration order. A failed stage stops the sequence: no later stage runs.</summary>
    /// <returns>The first stage failure, or success when every stage completed.</returns>
    public async Task<Result<Unit, StartupError>> RunAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < stages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stage = stages[index];
            StartupLog.StageStarting(logger, index + 1, stages.Count, stage.Name);

            var result = await stage.ExecuteAsync(cancellationToken);
            if (result is Fail<StartupError> fail)
            {
                StartupLog.StageFailed(logger, fail.Error.Exception, index + 1, stages.Count, stage.Name, fail.Error.Cause);
                return result;
            }

            StartupLog.StageComplete(logger, index + 1, stages.Count, stage.Name);
        }

        return new Ok<Unit>(default);
    }
}
