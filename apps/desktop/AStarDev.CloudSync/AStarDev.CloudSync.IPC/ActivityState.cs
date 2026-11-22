namespace AStarDev.CloudSync.IPC;

/// <summary>What the daemon is doing for one sync job, in the trust states of doc 13.</summary>
public enum ActivityState
{
    /// <summary>Listing the selected folders for the first time. Progress is counted in items indexed.</summary>
    InitialIndexing,

    /// <summary>Applying changes after the first listing.</summary>
    Working,

    /// <summary>Held back by the provider, for example by a rate limit or a network drop, and trying again after <see cref="ActivityView.RetryInSeconds"/>.</summary>
    Waiting,

    /// <summary>Nothing to do: everything selected is indexed and up to date.</summary>
    Healthy
}
