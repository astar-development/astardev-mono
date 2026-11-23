namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetHistory"/>.</summary>
/// <param name="Entries">The finished operations that passed the filters, newest first; empty when none did.</param>
public sealed record HistoryView(IReadOnlyList<HistoryEntryView> Entries);
