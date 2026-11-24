using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetReclassifyStatus"/> from the <see cref="ReclassifyTracker"/> and the last recorded run.</summary>
internal sealed class ReclassifyStatusQuery(ReclassifyTracker tracker, IReclassifyRunStore runs)
{
    /// <summary>Reads whether a run is active, how far it has got, and when the last one finished.</summary>
    /// <returns>The status, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ReclassifyStatusView, IpcError>> HandleAsync(GetReclassifyStatus query, CancellationToken cancellationToken)
        => await runs.GetLastCompletedAsync(cancellationToken) switch
        {
            Ok<Option<ReclassifyRun>> last => new Ok<ReclassifyStatusView>(new ReclassifyStatusView(tracker.Running, tracker.FilesDone, last.Value.Value is Some<ReclassifyRun> run ? run.Value.CompletedUtc : null)),
            Fail<ClassificationError> fail => new Fail<IpcError>(new HandlerFailed(ClassificationErrorMessages.Describe(fail.Error, "read", "last reclassify run")))
        };
}
