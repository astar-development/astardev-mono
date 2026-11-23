namespace AStarDev.CloudSync.IPC;

/// <summary>The reconciliation pipeline stage the daemon is in, in execution order. An idle daemon reports <see cref="ExecuteQueue"/>, the last stage.</summary>
public enum DashboardStage
{
    /// <summary>Reading the local file tree.</summary>
    EnumerateLocalState,

    /// <summary>Reading the provider's file tree, which is what the initial crawl does.</summary>
    EnumerateRemoteState,

    /// <summary>Assembling what was last known to be in sync.</summary>
    BuildKnownState,

    /// <summary>Comparing local, remote and known state, which is what applying provider changes does.</summary>
    AnalyseChanges,

    /// <summary>Turning the differences into queue operations.</summary>
    GenerateOperations,

    /// <summary>Running the queued operations.</summary>
    ExecuteQueue
}
