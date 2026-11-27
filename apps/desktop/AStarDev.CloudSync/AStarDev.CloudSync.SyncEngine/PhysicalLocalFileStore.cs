using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary><see cref="ILocalFileStore"/> over the real disk. Every exception the disk raises, such as access denied or a file locked by another process, becomes a <see cref="Permanent"/> failure, so the operation dead-letters and the queue goes on. A failure reading the incoming stream, such as a dropped connection, is <see cref="Transient"/> so the operation retries.</summary>
internal sealed class PhysicalLocalFileStore : ILocalFileStore
{
    private const int BufferSize = 81920;
    private const string TemporarySuffix = ".cloudsync-tmp";

    /// <inheritdoc/>
    public bool Exists(string root, RelativePath path) => Resolve(root, path) is { } full && File.Exists(full);

    /// <inheritdoc/>
    public Result<Stream, ProviderError> OpenRead(string root, RelativePath path)
        => Try.Run<Stream>(() => new FileStream(Inside(root, path), FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true)).ToResult(ToFailure);

    /// <inheritdoc/>
    public async Task<Result<Unit, ProviderError>> WriteAsync(string root, RelativePath path, Stream content, DateTimeOffset modifiedUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(token => ReplaceAsync(Inside(root, path), content, modifiedUtc, token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToFailure) switch
        {
            Ok<Result<Unit, ProviderError>> ok => ok.Value,
            Fail<ProviderError> fail => fail
        };
    }

    /// <inheritdoc/>
    public Result<Unit, ProviderError> Move(string root, RelativePath from, RelativePath to)
        => Try.Run(() => MoveFile(Inside(root, from), Inside(root, to))).ToResult(ToFailure);

    /// <inheritdoc/>
    public Result<Unit, ProviderError> Delete(string root, RelativePath path)
        => Try.Run(() => DeleteFile(Inside(root, path))).ToResult(ToFailure);

    private static string? Resolve(string root, RelativePath path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(fullRoot, path.Value));

        return full.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    internal static string Inside(string root, RelativePath path) => Resolve(root, path) ?? throw new UnauthorizedAccessException($"'{path.Value}' is outside the sync root.").WithHelpLink(SyncEngineErrorCodes.PathOutsideRoot);

    private static async Task<Result<Unit, ProviderError>> ReplaceAsync(string target, Stream content, DateTimeOffset modifiedUtc, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}{TemporarySuffix}");

        try
        {
            Result<Unit, ProviderError> copied;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                copied = await CopyAsync(content, output, cancellationToken).ConfigureAwait(false);

            if (copied.Value is not Ok<Unit>) return copied;

            File.SetLastWriteTimeUtc(temporary, modifiedUtc.UtcDateTime);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        return new Ok<Unit>(default);
    }

    private static async Task<Result<Unit, ProviderError>> CopyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        while (true)
        {
            var read = await Try.RunAsync(token => source.ReadAsync(buffer, token).AsTask(), cancellationToken).ConfigureAwait(false);

            switch (read.ToResult(exception => (ProviderError)new Transient(exception.Message)))
            {
                case Fail<ProviderError> fail:
                    return fail;
                case Ok<int> { Value: 0 }:
                    return new Ok<Unit>(default);
                case Ok<int> ok:
                    await destination.WriteAsync(buffer.AsMemory(0, ok.Value), cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private static Unit MoveFile(string from, string to)
    {
        if (!File.Exists(from) && File.Exists(to)) return default;

        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Move(from, to, overwrite: false);

        return default;
    }

    private static Unit DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        return default;
    }

    private static ProviderError ToFailure(Exception exception) => new Permanent(exception.Message);
}
