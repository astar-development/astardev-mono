using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Runs the local observer of a job in the background, at most one per job. Stops on request or when the process shuts down.</summary>
public sealed class LocalObservationRunner : IAsyncDisposable
{
    private readonly Dictionary<SyncJobId, RunningObserver> running = [];
    private readonly ISyncJobStore jobs;
    private readonly LocalObserver observer;
    private readonly ILogger<LocalObservationRunner> logger;

    internal LocalObservationRunner(ISyncJobStore jobs, LocalObserver observer, ILogger<LocalObservationRunner> logger)
    {
        this.jobs = jobs;
        this.observer = observer;
        this.logger = logger;
    }

    /// <summary>Starts observing the job's local root: a scan of the whole root, then the watcher. Returns before the scan finishes.</summary>
    /// <param name="syncJobId">The job to observe.</param>
    /// <param name="cancellationToken">Cancelling abandons the start, not an observer already running.</param>
    /// <returns>Success once the observer runs, also when it was already running, so calling twice is safe; <see cref="SyncJobNotFound"/> when the job does not exist; or <see cref="StorageFailed"/>. An observer that cannot watch or scan the root ends and is logged; the job's sync is not affected.</returns>
    public async Task<Result<Unit, StoreError>> StartAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
        => await jobs.GetAsync(syncJobId, cancellationToken) switch
        {
            Ok<SyncJob> ok => Launch(ok.Value),
            Fail<StoreError> fail => fail
        };

    /// <summary>Stops observing the job and waits for the observer to end. Stored observations stay.</summary>
    /// <param name="syncJobId">The job to stop. Nothing happens when it is not running.</param>
    /// <param name="cancellationToken">Cancelling stops the waiting, not the stop.</param>
    public async Task StopAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        RunningObserver? current;
        lock (running) running.TryGetValue(syncJobId, out current);
        if (current is null) return;

        await current.Cancellation.CancelAsync();
        await Task.WhenAny(current.Task).WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        RunningObserver[] observers;
        lock (running) observers = [.. running.Values];

        foreach (var current in observers) await current.Cancellation.CancelAsync();

        await Task.WhenAll(observers.Select(current => Task.WhenAny(current.Task)));
    }

    private Ok<Unit> Launch(SyncJob job)
    {
        lock (running)
        {
            if (running.ContainsKey(job.Id)) return new Ok<Unit>(default);

            var cancellation = new CancellationTokenSource();
            running[job.Id] = new RunningObserver(cancellation, Task.Run(() => RunAsync(job, cancellation.Token), CancellationToken.None));
        }

        return new Ok<Unit>(default);
    }

    private async Task RunAsync(SyncJob job, CancellationToken cancellationToken)
    {
        try
        {
            if (await observer.RunAsync(job, cancellationToken) is Fail<LocalObservationError> fail) EngineLog.LocalObservationEnded(logger, job.Id.Value, fail.Error.Value);
        }
        finally
        {
            lock (running) running.Remove(job.Id);
        }
    }

    private sealed record RunningObserver(CancellationTokenSource Cancellation, Task Task);
}
