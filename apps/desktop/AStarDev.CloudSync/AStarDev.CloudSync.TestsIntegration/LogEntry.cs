using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed record LogEntry(string Category, LogLevel Level, int EventId, string Message, IReadOnlyDictionary<string, object?> State, IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes)
{
    public object? ScopeValue(string key) => Scopes.Where(scope => scope.ContainsKey(key)).Select(scope => scope[key]).FirstOrDefault();
}
