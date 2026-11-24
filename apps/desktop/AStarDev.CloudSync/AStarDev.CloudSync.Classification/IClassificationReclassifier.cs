namespace AStarDev.CloudSync.Classification;

/// <summary>Recomputes the automatic links of every stored file. User links and ignored terms are untouched. A rerun is safe: it starts over and gives the same result.</summary>
public interface IClassificationReclassifier
{
    /// <summary>Runs a reclassify now, whether or not one is needed. Only one run is active at a time.</summary>
    /// <param name="progress">Receives a report after each batch of files, so a display never looks frozen.</param>
    /// <param name="cancellationToken">Cancels the run; the batches already written stay, and the run is not recorded.</param>
    /// <returns>What happened. A second call while a run is active returns <see cref="ReclassifyAlreadyRunning"/>.</returns>
    Task<ReclassifyOutcome> StartAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken);

    /// <summary>Runs a reclassify only when no run was ever recorded or the recorded one used an older seed than the one now applied.</summary>
    /// <param name="progress">Receives a report after each batch of files.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What happened; <see cref="ReclassifyNotNeeded"/> when the last run is current.</returns>
    Task<ReclassifyOutcome> StartIfStaleAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken);

    /// <summary>Completes when no run is active, at once when none is. A cancelled wait propagates as <see cref="OperationCanceledException"/>.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    Task WaitUntilIdleAsync(CancellationToken cancellationToken);
}
