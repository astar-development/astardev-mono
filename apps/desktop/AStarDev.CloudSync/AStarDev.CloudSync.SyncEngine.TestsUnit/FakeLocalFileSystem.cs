using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class FakeLocalFileSystem : ILocalFileSystem
{
    private readonly Dictionary<string, LocalFile> files = [];
    private Action<LocalFileEvent>? handler;

    public List<string> Listed { get; } = [];

    public int Fingerprinted { get; private set; }

    public LocalObservationError? WatchFailure { get; set; }

    public bool Watching => handler is not null;

    public void Put(string path, long size, DateTimeOffset modifiedUtc) => files[path] = new LocalFile(new RelativePath(path), size, modifiedUtc, null);

    public void Remove(string path) => files.Remove(path);

    public void Raise(LocalFileEvent fileEvent) => handler?.Invoke(fileEvent);

    public Task<Result<IReadOnlyList<LocalFile>, LocalObservationError>> ListFilesAsync(string root, RelativePath subtree, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Listed.Add(subtree.Value ?? string.Empty);
        IReadOnlyList<LocalFile> found = [.. files.Values.Where(file => string.IsNullOrEmpty(subtree.Value) || file.Path.Value == subtree.Value || file.Path.Value.StartsWith(subtree.Value + "/", StringComparison.Ordinal))];

        return Task.FromResult<Result<IReadOnlyList<LocalFile>, LocalObservationError>>(new Ok<IReadOnlyList<LocalFile>>(found));
    }

    public Task<Result<ContentFingerprint, LocalObservationError>> FingerprintAsync(string root, RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Fingerprinted++;

        return Task.FromResult<Result<ContentFingerprint, LocalObservationError>>(new Ok<ContentFingerprint>(new ContentFingerprint("fp-" + path.Value)));
    }

    public Result<IDisposable, LocalObservationError> Watch(string root, Action<LocalFileEvent> onEvent)
    {
        if (WatchFailure is { } failure) return new Fail<LocalObservationError>(failure);

        handler = onEvent;

        return new Ok<IDisposable>(new Stop(this));
    }

    private sealed class Stop(FakeLocalFileSystem owner) : IDisposable
    {
        public void Dispose() => owner.handler = null;
    }
}
