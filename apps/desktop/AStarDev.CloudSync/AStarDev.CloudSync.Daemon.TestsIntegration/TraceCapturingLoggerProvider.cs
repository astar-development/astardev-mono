using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

internal sealed class TraceCapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, string Message, ActivityTraceId? TraceId)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<(string Category, string Message, ActivityTraceId? TraceId)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue((category, formatter(state, exception), Activity.Current?.TraceId));
    }
}
