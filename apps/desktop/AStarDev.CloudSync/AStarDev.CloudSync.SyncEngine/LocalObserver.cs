using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Watches one job's local root and records what it sees as <c>Local</c> snapshots. It decides nothing: no operation is created and no file is touched. A file outside every scope of the job is not recorded, so it can never be uploaded.
/// It scans the whole root at start, scans the changed paths once the disk has been quiet for <see cref="LocalObservationOptions.Debounce"/>, and scans the whole root again after the watcher overflows.
/// </summary>
internal sealed class LocalObserver(ILocalFileSystem fileSystem, ILocalObservationStore store, ISyncScopeStore scopes, IOptions<LocalObservationOptions> options, TimeProvider timeProvider, ILogger<LocalObserver> logger)
{
    private const int BatchSize = 500;
    private const int MaxFingerprintParallelism = 4;
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>Observes the job until cancelled.</summary>
    /// <param name="job">The job whose <see cref="SyncJob.LocalRootPath"/> to observe.</param>
    /// <param name="cancellationToken">Cancelling stops the watcher and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Only a failure: <see cref="FileSystemFailed"/> when the root cannot be watched or the first scan cannot read it, or <see cref="ObservationStoreFailed"/> when the first scan cannot be stored. A later scan that fails is logged and the whole root is scanned again at the next change.</returns>
    public async Task<Result<Unit, LocalObservationError>> RunAsync(SyncJob job, CancellationToken cancellationToken)
    {
        var pending = new PendingChanges();

        return fileSystem.Watch(job.LocalRootPath, pending.Add) switch
        {
            Ok<IDisposable> watching => await ObserveAsync(job, pending, watching.Value, cancellationToken),
            Fail<LocalObservationError> fail => fail
        };
    }

    /// <summary>Compares the disk with what is stored for a subtree and records the difference.</summary>
    /// <param name="job">The job whose root to scan.</param>
    /// <param name="subtree">A file or folder relative to the root; empty scans the whole root.</param>
    /// <param name="cancellationToken">Cancelling stops before the next batch is stored and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success, also when nothing changed, so scanning twice is safe; or the reason the disk or the store failed, including the scopes of the job that cannot be read. A file that cannot be read for its digest is logged and left for the next scan.</returns>
    public async Task<Result<Unit, LocalObservationError>> ScanAsync(SyncJob job, RelativePath subtree, CancellationToken cancellationToken)
        => await scopes.ListAsync(job.Id, cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncScope>> selected => await ListAsync(job, subtree, selected.Value, cancellationToken),
            Fail<StoreError> fail => new Fail<LocalObservationError>(new ObservationStoreFailed(fail.Error))
        };

    private async Task<Result<Unit, LocalObservationError>> ListAsync(SyncJob job, RelativePath subtree, IReadOnlyList<SyncScope> selected, CancellationToken cancellationToken)
        => await fileSystem.ListFilesAsync(job.LocalRootPath, subtree, cancellationToken) switch
        {
            Ok<IReadOnlyList<LocalFile>> ok => await CompareAsync(job, subtree, [.. ok.Value.Where(file => ScopeCoverage.Covers(selected, file.Path))], selected, cancellationToken),
            Fail<LocalObservationError> fail => fail
        };

    private async Task<Result<Unit, LocalObservationError>> CompareAsync(SyncJob job, RelativePath subtree, IReadOnlyList<LocalFile> disk, IReadOnlyList<SyncScope> selected, CancellationToken cancellationToken)
        => await store.ListPresentAsync(job.Id, subtree, cancellationToken) switch
        {
            Ok<IReadOnlyList<LocalSnapshot>> ok => await ApplyDifferenceAsync(job, disk, ok.Value.Where(snapshot => ScopeCoverage.Covers(selected, snapshot.Path)).ToDictionary(snapshot => snapshot.Path), cancellationToken),
            Fail<StoreError> fail => new Fail<LocalObservationError>(new ObservationStoreFailed(fail.Error))
        };

    private async Task<Result<Unit, LocalObservationError>> ApplyDifferenceAsync(SyncJob job, IReadOnlyList<LocalFile> disk, Dictionary<RelativePath, LocalSnapshot> stored, CancellationToken cancellationToken)
    {
        var present = await FingerprintChangedAsync(job, disk, stored, cancellationToken);
        var onDisk = disk.Select(file => file.Path).ToHashSet();
        var deleted = stored.Keys.Where(path => !onDisk.Contains(path)).ToList();

        return await RecordAsync(job, present, deleted, cancellationToken);
    }

    private async Task<Result<Unit, LocalObservationError>> ObserveAsync(SyncJob job, PendingChanges pending, IDisposable watching, CancellationToken cancellationToken)
    {
        using var stopWatching = watching;
        if (await ScanAsync(job, default, cancellationToken) is Fail<LocalObservationError> failure) return failure;

        while (true)
        {
            await pending.WaitAsync(cancellationToken);
            if (!pending.HasChanges) continue;

            await QuietAsync(pending, cancellationToken);
            var (overflowed, paths) = pending.Take();
            if (await ScanChangesAsync(job, overflowed, paths, cancellationToken) is Fail<LocalObservationError> failed)
            {
                EngineLog.LocalObservationFailed(logger, job.Id.Value, failed.Error.Value, ErrorInfo.CodeOf(failed.Error.Value));
                pending.RequestRescan();
            }
        }
    }

    private async Task QuietAsync(PendingChanges pending, CancellationToken cancellationToken)
    {
        long seen;

        do
        {
            seen = pending.Count;
            await Task.Delay(options.Value.Debounce > TimeSpan.Zero ? options.Value.Debounce : TimeSpan.Zero, timeProvider, cancellationToken);
        }
        while (pending.Count != seen);
    }

    private async Task<Result<Unit, LocalObservationError>> ScanChangesAsync(SyncJob job, bool overflowed, IReadOnlyList<RelativePath> paths, CancellationToken cancellationToken)
    {
        if (overflowed) return await ScanAsync(job, default, cancellationToken);

        foreach (var path in paths)
        {
            if (await ScanAsync(job, path, cancellationToken) is Fail<LocalObservationError> failure) return failure;
        }

        return new Ok<Unit>(default);
    }

    private static bool SameInstant(DateTimeOffset stored, DateTimeOffset onDisk) => (stored - onDisk).Duration() < TimestampTolerance;

    private async Task<List<LocalSnapshot>> FingerprintChangedAsync(SyncJob job, IReadOnlyList<LocalFile> disk, Dictionary<RelativePath, LocalSnapshot> stored, CancellationToken cancellationToken)
    {
        var changed = disk.Where(file => !stored.TryGetValue(file.Path, out var last) || last.Size != file.Size || !SameInstant(last.ModifiedUtc, file.ModifiedUtc) || last.Identity != file.Identity).ToList();
        var fingerprints = new Result<ContentFingerprint, LocalObservationError>[changed.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Parallelism(options.Value), CancellationToken = cancellationToken };
        await Parallel.ForAsync(0, changed.Count, parallel, async (index, token) => fingerprints[index] = await fileSystem.FingerprintAsync(job.LocalRootPath, changed[index].Path, token));
        var present = new List<LocalSnapshot>();

        for (var index = 0; index < changed.Count; index++)
        {
            var file = changed[index];
            switch (fingerprints[index])
            {
                case Ok<ContentFingerprint> ok:
                    present.Add(new LocalSnapshot(file.Path, file.Size, file.ModifiedUtc, file.Identity, ok.Value));
                    break;
                case Fail<LocalObservationError> fail:
                    SyncLog.TemporaryFileLock(logger, file.Path.Value, job.Id.Value, fail.Error.Value?.ToString() ?? string.Empty, ErrorInfo.CodeOf(fail.Error.Value));
                    break;
            }
        }

        return present;
    }

    private static int Parallelism(LocalObservationOptions observation) => Math.Clamp(observation.FingerprintParallelism, 1, MaxFingerprintParallelism);

    private async Task<Result<Unit, LocalObservationError>> RecordAsync(SyncJob job, List<LocalSnapshot> present, List<RelativePath> deleted, CancellationToken cancellationToken)
    {
        var observedUtc = timeProvider.GetUtcNow();

        foreach (var chunk in present.Chunk(BatchSize))
        {
            if (await store.RecordAsync(job.Id, chunk, [], observedUtc, cancellationToken) is Fail<StoreError> failure) return new Fail<LocalObservationError>(new ObservationStoreFailed(failure.Error));
        }

        foreach (var chunk in deleted.Chunk(BatchSize))
        {
            if (await store.RecordAsync(job.Id, [], chunk, observedUtc, cancellationToken) is Fail<StoreError> failure) return new Fail<LocalObservationError>(new ObservationStoreFailed(failure.Error));
        }

        return new Ok<Unit>(default);
    }

    private sealed class PendingChanges
    {
        private readonly object gate = new();
        private readonly HashSet<RelativePath> paths = [];
        private readonly SemaphoreSlim signal = new(0);
        private bool overflowed;
        private long count;

        public long Count => Volatile.Read(ref count);

        public bool HasChanges
        {
            get
            {
                lock (gate) return overflowed || paths.Count > 0;
            }
        }

        public void Add(LocalFileEvent fileEvent)
        {
            lock (gate)
            {
                switch (fileEvent.Value)
                {
                    case Touched touched:
                        paths.Add(touched.Path);
                        break;
                    case Overflowed:
                        overflowed = true;
                        break;
                }

                count++;
                signal.Release();
            }
        }

        public void RequestRescan()
        {
            lock (gate) overflowed = true;
        }

        public Task WaitAsync(CancellationToken cancellationToken) => signal.WaitAsync(cancellationToken);

        public (bool Overflowed, IReadOnlyList<RelativePath> Paths) Take()
        {
            lock (gate)
            {
                var taken = (overflowed, (IReadOnlyList<RelativePath>)[.. paths]);
                overflowed = false;
                paths.Clear();
                while (signal.Wait(0)) { }

                return taken;
            }
        }
    }
}
