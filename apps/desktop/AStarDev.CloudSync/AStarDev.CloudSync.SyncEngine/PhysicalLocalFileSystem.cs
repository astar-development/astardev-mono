using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary><see cref="ILocalFileSystem"/> over the real disk, watching with <see cref="FileSystemWatcher"/>. The filesystem identity comes from the <see cref="ILocalIdentityReader"/>, so <see cref="LocalFile.Identity"/> is null where the reader has none.</summary>
internal sealed class PhysicalLocalFileSystem(ILocalIdentityReader identityReader) : ILocalFileSystem
{
    private const int HashBufferSize = 81920;
    private const int WatcherBufferSize = 65536;
    private const string RedactedRoot = "<local root>";

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<LocalFile>, LocalObservationError>> ListFilesAsync(string root, RelativePath subtree, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root)) return new Fail<LocalObservationError>(new FileSystemFailed("The local root does not exist."));

        var outcome = await Try.RunAsync(token => Task.Run(() => List(root, subtree, token), token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => ToFailure(root, exception));
    }

    /// <inheritdoc/>
    public async Task<Result<ContentFingerprint, LocalObservationError>> FingerprintAsync(string root, RelativePath path, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(token => HashAsync(Path.Combine(root, path.Value), token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => ToFailure(root, exception));
    }

    /// <inheritdoc/>
    /// <remarks>Created, changed and deleted raise <see cref="Touched"/> for the path; renamed raises it for the old and the new path; the watcher's error event, which is how a full buffer is reported, raises <see cref="Overflowed"/>.</remarks>
    public Result<IDisposable, LocalObservationError> Watch(string root, Action<LocalFileEvent> onEvent)
        => Try.Run<IDisposable>(() => Start(root, onEvent)).ToResult(exception => ToFailure(root, exception));

    private IReadOnlyList<LocalFile> List(string root, RelativePath subtree, CancellationToken cancellationToken)
    {
        var full = string.IsNullOrEmpty(subtree.Value) ? root : Path.Combine(root, subtree.Value);
        if (File.Exists(full)) return Describe(root, new FileInfo(full)) is { } single ? [single] : [];
        if (!Directory.Exists(full)) return [];

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };
        var files = new List<LocalFile>();

        foreach (var file in new DirectoryInfo(full).EnumerateFiles("*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Describe(root, file) is { } described) files.Add(described);
        }

        return files;
    }

    private LocalFile? Describe(string root, FileInfo file)
        => file.Attributes.HasFlag(FileAttributes.ReparsePoint) ? null : new LocalFile(ToRelative(root, file.FullName), file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), identityReader.Read(file.FullName));

    private static RelativePath ToRelative(string root, string fullPath) => new(Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/'));

    private static async Task<ContentFingerprint> HashAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, HashBufferSize, useAsync: true);
        var hash = new QuickXorHash();
        var buffer = new byte[HashBufferSize];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0) hash.Append(buffer.AsSpan(0, read));

        return hash.Finish();
    }

    private static FileSystemWatcher Start(string root, Action<LocalFileEvent> onEvent)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = WatcherBufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Created += (_, args) => onEvent(new Touched(ToRelative(root, args.FullPath)));
        watcher.Changed += (_, args) => onEvent(new Touched(ToRelative(root, args.FullPath)));
        watcher.Deleted += (_, args) => onEvent(new Touched(ToRelative(root, args.FullPath)));
        watcher.Renamed += (_, args) =>
        {
            onEvent(new Touched(ToRelative(root, args.OldFullPath)));
            onEvent(new Touched(ToRelative(root, args.FullPath)));
        };
        watcher.Error += (_, _) => onEvent(new Overflowed());
        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    private static LocalObservationError ToFailure(string root, Exception exception) => new FileSystemFailed(exception.Message.Replace(root, RedactedRoot, StringComparison.Ordinal));
}
