using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The local disk as executors change it: read, replace, move and delete files under a job's root. A port so executors can be tested without a disk. Failure is a <see cref="Permanent"/> value, never an exception; cancellation propagates.</summary>
internal interface ILocalFileStore
{
    /// <summary>Tells whether a regular file exists.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="path">The file, relative to the root.</param>
    /// <returns><see langword="false"/> also when the path would leave the root.</returns>
    bool Exists(string root, RelativePath path);

    /// <summary>Opens a file for reading, sharing it with readers only.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="path">The file, relative to the root.</param>
    /// <returns>A seekable stream the caller disposes, or <see cref="Permanent"/> when the file is missing, locked, access is denied, or the path leaves the root.</returns>
    Result<Stream, ProviderError> OpenRead(string root, RelativePath path);

    /// <summary>Writes content to a temporary file in the target folder, gives it the modified time, then moves it over the target in one step, so a reader sees the old file or the whole new one.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="path">The file to create or replace, relative to the root. Missing folders are created.</param>
    /// <param name="content">The bytes to write; read to the end, not disposed.</param>
    /// <param name="modifiedUtc">The last-modified time the file gets.</param>
    /// <param name="cancellationToken">Cancelling abandons the write, removes the temporary file and leaves the target as it was.</param>
    /// <returns>Success, or <see cref="Permanent"/> when the disk refuses; the target is then untouched and no temporary file is left.</returns>
    Task<Result<Unit, ProviderError>> WriteAsync(string root, RelativePath path, Stream content, DateTimeOffset modifiedUtc, CancellationToken cancellationToken);

    /// <summary>Moves a file to a new path without replacing anything.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="from">The file, relative to the root.</param>
    /// <param name="to">The new path, relative to the root. Missing folders are created.</param>
    /// <returns>Success, also when the file is already at the new path and nothing is left at the old one; or <see cref="Permanent"/> when the target is occupied, the file is locked or access is denied.</returns>
    Result<Unit, ProviderError> Move(string root, RelativePath from, RelativePath to);

    /// <summary>Deletes a file.</summary>
    /// <param name="root">Absolute path of the job's local root.</param>
    /// <param name="path">The file, relative to the root.</param>
    /// <returns>Success, also when the file is already gone, so calling twice is safe; or <see cref="Permanent"/> when the file is locked or access is denied.</returns>
    Result<Unit, ProviderError> Delete(string root, RelativePath path);
}
