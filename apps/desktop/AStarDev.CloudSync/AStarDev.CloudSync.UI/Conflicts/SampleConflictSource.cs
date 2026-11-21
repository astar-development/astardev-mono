namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>Placeholder conflicts until the daemon reports real ones. Fixed values, so the screen looks the same every run.</summary>
internal sealed class SampleConflictSource : IConflictSource
{
    private static readonly DateTimeOffset Reference = new(2026, 11, 20, 9, 30, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public bool IsSampleData => true;

    /// <inheritdoc/>
    public IReadOnlyList<ConflictItem> GetConflicts() =>
    [
        new("sample-1", "Budget 2026.xlsx", TimeSpan.FromDays(3), ConflictStatus.Unresolved, ConflictReason.BothModified, new("/home/you/OneDrive/Finance/Budget 2026.xlsx", Reference.AddHours(-30), 48_128), new("/Finance/Budget 2026.xlsx", Reference.AddHours(-20), 51_200)),
        new("sample-2", "Holiday plan.docx", TimeSpan.FromHours(5), ConflictStatus.Unresolved, ConflictReason.DeletedLocallyModifiedRemotely, new("/home/you/OneDrive/Documents/Holiday plan.docx", Reference.AddDays(-9), 0), new("/Documents/Holiday plan.docx", Reference.AddHours(-6), 22_528)),
        new("sample-3", "Notes.md", TimeSpan.FromDays(12), ConflictStatus.Deferred, ConflictReason.DeletedRemotelyModifiedLocally, new("/home/you/OneDrive/Notes.md", Reference.AddDays(-1), 4_096), new("/Notes.md", Reference.AddDays(-12), 0))
    ];
}
