namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>Why CloudSync could not reconcile the item by itself. Each value has a localised explanation under <c>Conflicts.Reason.&lt;value&gt;</c>.</summary>
internal enum ConflictReason
{
    /// <summary>Both the local and the remote copy changed since the last sync.</summary>
    BothModified,

    /// <summary>The local copy was deleted while the remote copy changed.</summary>
    DeletedLocallyModifiedRemotely,

    /// <summary>The remote copy was deleted while the local copy changed.</summary>
    DeletedRemotelyModifiedLocally
}
