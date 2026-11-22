using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class RecordingObservationStore : ILocalObservationStore
{
    public List<LocalSnapshot> Present { get; } = [];

    public List<(IReadOnlyList<LocalSnapshot> Present, IReadOnlyList<RelativePath> Deleted)> Recorded { get; } = [];

    public Task<Result<IReadOnlyList<LocalSnapshot>, StoreError>> ListPresentAsync(SyncJobId jobId, RelativePath subtree, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LocalSnapshot> found = [.. Present.Where(snapshot => string.IsNullOrEmpty(subtree.Value) || snapshot.Path.Value == subtree.Value || snapshot.Path.Value.StartsWith(subtree.Value + "/", StringComparison.Ordinal))];

        return Task.FromResult<Result<IReadOnlyList<LocalSnapshot>, StoreError>>(new Ok<IReadOnlyList<LocalSnapshot>>(found));
    }

    public Task<Result<Unit, StoreError>> RecordAsync(SyncJobId jobId, IReadOnlyList<LocalSnapshot> present, IReadOnlyList<RelativePath> deleted, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Recorded.Add((present, deleted));
        Present.RemoveAll(snapshot => present.Any(seen => seen.Path == snapshot.Path) || deleted.Contains(snapshot.Path));
        Present.AddRange(present);

        return Task.FromResult<Result<Unit, StoreError>>(new Ok<Unit>(default));
    }
}
