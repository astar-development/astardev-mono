using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>A file the local disk showed, without its content digest.</summary>
/// <param name="Path">Where it is, relative to the job's local root, with <c>/</c> separators and no leading slash.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">Last-modified time of the content.</param>
/// <param name="Identity">The filesystem identity; null when the file system port cannot read one.</param>
internal sealed record LocalFile(RelativePath Path, long Size, DateTimeOffset ModifiedUtc, LocalIdentity? Identity);
