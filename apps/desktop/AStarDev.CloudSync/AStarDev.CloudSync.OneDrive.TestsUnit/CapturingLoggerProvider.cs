using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> entries = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (entries) return [.. entries];
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        private static readonly AsyncLocal<ScopeNode?> Current = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var node = new ScopeNode(Current.Value, ToDictionary(state));
            Current.Value = node;

            return new Popper(node);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<IReadOnlyDictionary<string, object?>>();
            for (var node = Current.Value; node is not null; node = node.Parent) scopes.Add(node.Values);

            lock (owner.entries) owner.entries.Add(new LogEntry(category, logLevel, eventId.Id, formatter(state, exception), ToDictionary(state), scopes));
        }

        private static Dictionary<string, object?> ToDictionary<TState>(TState state)
            => state is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value) : [];

        private sealed class Popper(ScopeNode node) : IDisposable
        {
            public void Dispose() => Current.Value = node.Parent;
        }
    }

    private sealed record ScopeNode(ScopeNode? Parent, IReadOnlyDictionary<string, object?> Values);
}
