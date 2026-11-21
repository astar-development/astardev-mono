using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Observability.TestsUnit;

internal sealed class CapturingLogger : ILogger
{
    public List<object?> Scopes { get; } = [];

    public int DisposedScopes { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        Scopes.Add(state);

        return new ScopeToken(this);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }

    private sealed class ScopeToken(CapturingLogger owner) : IDisposable
    {
        public void Dispose() => owner.DisposedScopes++;
    }
}
