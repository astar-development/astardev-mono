namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>Where the Conflicts screen gets its conflicts. A daemon-backed implementation replaces <see cref="SampleConflictSource"/> without changing the view.</summary>
internal interface IConflictSource
{
    /// <summary>True when the conflicts are invented for display, so the screen must say so.</summary>
    bool IsSampleData { get; }

    /// <summary>The open conflicts, oldest first. Empty when there are none; never <see langword="null"/>.</summary>
    IReadOnlyList<ConflictItem> GetConflicts();
}
