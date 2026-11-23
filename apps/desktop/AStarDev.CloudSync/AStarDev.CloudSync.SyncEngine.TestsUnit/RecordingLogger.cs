using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(int EventId, LogLevel Level)> entries = [];

    public IReadOnlyList<(int EventId, LogLevel Level)> Entries
    {
        get
        {
            lock (entries) return [.. entries];
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (entries) entries.Add((eventId.Id, logLevel));
    }
}
