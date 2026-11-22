using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class OverflowingFileSystem(ILocalFileSystem inner) : ILocalFileSystem
{
    private Action<LocalFileEvent>? handler;

    public void Overflow() => handler?.Invoke(new Overflowed());

    public Task<Result<IReadOnlyList<LocalFile>, LocalObservationError>> ListFilesAsync(string root, RelativePath subtree, CancellationToken cancellationToken)
        => inner.ListFilesAsync(root, subtree, cancellationToken);

    public Task<Result<ContentFingerprint, LocalObservationError>> FingerprintAsync(string root, RelativePath path, CancellationToken cancellationToken)
        => inner.FingerprintAsync(root, path, cancellationToken);

    public Result<IDisposable, LocalObservationError> Watch(string root, Action<LocalFileEvent> onEvent)
    {
        handler = onEvent;

        return new Ok<IDisposable>(new Stop());
    }

    private sealed class Stop : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
