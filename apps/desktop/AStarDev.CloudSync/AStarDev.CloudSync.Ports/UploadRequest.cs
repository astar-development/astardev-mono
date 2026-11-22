using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>The content of one file to place at a remote path, with the modified time the remote copy must carry.</summary>
/// <param name="Path">Where the file goes, relative to the remote root. Missing folders on the way are created; a file already there is replaced.</param>
/// <param name="Content">The bytes to send. Must be readable and seekable with its position at the start, because its <see cref="Stream.Length"/> decides between a single request and chunks. The provider reads it and never disposes it.</param>
/// <param name="ModifiedUtc">The last-modified time the remote copy is given, so the next remote observation matches the local file.</param>
public sealed record UploadRequest(RelativePath Path, Stream Content, DateTimeOffset ModifiedUtc);
