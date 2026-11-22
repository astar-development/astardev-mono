using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The conflict is not open, so there is nothing to resolve. It may already have been resolved. Nothing was changed.</summary>
/// <param name="Id">The conflict that was asked about.</param>
public sealed record ConflictNotOpen(ConflictId Id);

/// <summary>The item of the conflict is no longer known to the daemon, for example after a re-crawl. Nothing was changed.</summary>
/// <param name="Id">The conflict that was asked about.</param>
public sealed record ConflictItemGone(ConflictId Id);

/// <summary>The item still has queued, running or dead-lettered work, so a resolution would race it. Resolve once that work is done or dropped. Nothing was changed.</summary>
/// <param name="Id">The conflict that was asked about.</param>
public sealed record ConflictItemBusy(ConflictId Id);

/// <summary>The copy that keeps the local version could not be made, so the conflict stays open and the local file is untouched.</summary>
/// <param name="Id">The conflict that was asked about.</param>
/// <param name="Reason">Why the copy failed, suitable for logs.</param>
public sealed record KeepBothCopyFailed(ConflictId Id, string Reason);

/// <summary>A store could not be read or written. The conflict may be left partly resolved: queued work is kept, and the conflict is resolved again by asking once more.</summary>
/// <param name="Error">The store's failure.</param>
public sealed record ResolutionStoreFailed(StoreError Error);

/// <summary>Closed set of reasons a conflict could not be resolved.</summary>
public union ResolutionError(ConflictNotOpen, ConflictItemGone, ConflictItemBusy, KeepBothCopyFailed, ResolutionStoreFailed);
