using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Something under the watched root changed: a file or folder was created, changed, deleted, or moved from or to this path.</summary>
/// <param name="Path">The path that changed, relative to the root. A folder stands for everything below it.</param>
internal sealed record Touched(RelativePath Path);

/// <summary>The watcher lost events, so nothing it reported can be trusted to be complete and the whole root must be scanned again.</summary>
internal sealed record Overflowed;

/// <summary>Closed set of notifications a watcher raises.</summary>
internal union LocalFileEvent(Touched, Overflowed);
