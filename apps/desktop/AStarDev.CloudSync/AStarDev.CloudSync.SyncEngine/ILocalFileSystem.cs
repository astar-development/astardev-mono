using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The local disk as the observer sees it: list, fingerprint and watch. A port so the observer's rules can be tested without a disk. Failure is a value; cancellation propagates.</summary>
internal interface ILocalFileSystem
{
    /// <summary>Lists the regular files at or below a path.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="subtree">A file or folder relative to the root; empty means the whole root. A path that does not exist lists nothing and is not a failure, because a deletion is how it gets here.</param>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The files, or <see cref="FileSystemFailed"/> when the root itself cannot be read. Symbolic links are not listed.</returns>
    Task<Result<IReadOnlyList<LocalFile>, LocalObservationError>> ListFilesAsync(string root, RelativePath subtree, CancellationToken cancellationToken);

    /// <summary>Reads a file and computes its content digest.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="path">The file, relative to the root.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The digest, or <see cref="FileSystemFailed"/> when the file vanished or cannot be read.</returns>
    Task<Result<ContentFingerprint, LocalObservationError>> FingerprintAsync(string root, RelativePath path, CancellationToken cancellationToken);

    /// <summary>Starts raising events for changes under the root.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="onEvent">Called from any thread, possibly often and concurrently. It must return quickly.</param>
    /// <returns>A handle whose disposal stops the events, or <see cref="FileSystemFailed"/> when the root cannot be watched.</returns>
    Result<IDisposable, LocalObservationError> Watch(string root, Action<LocalFileEvent> onEvent);
}
