namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The six reconciliation pipeline stages, in execution order.</summary>
internal enum ReconciliationStage
{
    /// <summary>Reading the local file tree.</summary>
    EnumerateLocalState,

    /// <summary>Reading the provider's file tree.</summary>
    EnumerateRemoteState,

    /// <summary>Assembling what was last known to be in sync.</summary>
    BuildKnownState,

    /// <summary>Comparing local, remote and known state.</summary>
    AnalyseChanges,

    /// <summary>Turning the differences into queue operations.</summary>
    GenerateOperations,

    /// <summary>Running the queued operations.</summary>
    ExecuteQueue
}
