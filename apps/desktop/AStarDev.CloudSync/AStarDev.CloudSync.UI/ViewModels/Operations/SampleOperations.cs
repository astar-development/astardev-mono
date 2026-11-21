namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>Placeholder operations standing in for daemon data until it exists. Screens label them as sample data.</summary>
internal static class SampleOperations
{
    private const string Personal = "OneDrive";
    private const string Work = "OneDrive (work)";

    private static readonly DateTimeOffset Reference = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Every sample operation, newest first, covering every status and type.</summary>
    public static IReadOnlyList<OperationEntry> All { get; } =
    [
        new("op-01", Personal, OperationType.Download, OperationStatus.Running, Reference, "/Documents/report.docx", 1_204_000),
        new("op-02", Personal, OperationType.Upload, OperationStatus.Pending, Reference.AddMinutes(-5), "/Photos/2026/img1.jpg", 3_400_000),
        new("op-03", Work, OperationType.Rename, OperationStatus.Pending, Reference.AddMinutes(-9), "/Work/old.txt", 0),
        new("op-04", Personal, OperationType.QueueRun, OperationStatus.Running, Reference.AddMinutes(-12), "/", 0),
        new("op-05", Personal, OperationType.Download, OperationStatus.Completed, Reference.AddDays(-1), "/Documents/notes.md", 18_200),
        new("op-06", Personal, OperationType.Delete, OperationStatus.Completed, Reference.AddDays(-2), "/Temp/cache.bin", 920_000),
        new("op-07", Work, OperationType.Upload, OperationStatus.Failed, Reference.AddDays(-3), "/Work/big.iso", 4_700_000_000),
        new("op-08", Personal, OperationType.ConflictCreated, OperationStatus.Completed, Reference.AddDays(-4), "/Documents/plan.xlsx", 56_000),
        new("op-09", Work, OperationType.Download, OperationStatus.Completed, Reference.AddDays(-10), "/Work/specs.pdf", 2_300_000),
        new("op-10", Personal, OperationType.Rename, OperationStatus.Failed, Reference.AddDays(-12), "/Documents/draft.txt", 0)
    ];
}
