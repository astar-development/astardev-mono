namespace AStarDev.CloudSync.IPC;

/// <summary>One stored log message, ready to show as it is.</summary>
/// <param name="TimestampUtc">When the daemon logged it.</param>
/// <param name="Level">The <c>LogLevel</c> number: 0 Trace, 1 Debug, 2 Information, 3 Warning, 4 Error, 5 Critical.</param>
/// <param name="Source">The logger category that wrote it.</param>
/// <param name="Message">The rendered message with any path or e-mail address already replaced.</param>
public sealed record LogEntryView(DateTimeOffset TimestampUtc, int Level, string Source, string Message);
