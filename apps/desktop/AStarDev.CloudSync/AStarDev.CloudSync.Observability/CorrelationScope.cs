using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Observability;

/// <summary>Adds the correlation identifiers of doc 12 to every log written inside a scope.</summary>
public static class CorrelationScope
{
    private const string OperationIdKey = "OperationId";
    private const string ItemIdKey = "ItemId";
    private const string SyncJobIdKey = "SyncJobId";
    private const string AccountIdKey = "AccountId";
    private const string RemoteItemIdKey = "RemoteItemId";

    /// <summary>Begins a logging scope whose state carries each identifier that is not <see langword="null"/>; the others are left out, never written as empty values.</summary>
    /// <param name="logger">The logger whose scope is opened.</param>
    /// <param name="operationId">The queued operation being executed, when there is one.</param>
    /// <param name="itemId">The synchronised item the work concerns, when there is one.</param>
    /// <param name="syncJobId">The sync job the work belongs to, when there is one.</param>
    /// <param name="accountId">The account the work runs for, when there is one.</param>
    /// <param name="remoteItemId">The provider's identifier of the item the call concerns, when there is one.</param>
    /// <returns>The scope; dispose it to end it. Never <see langword="null"/>, so it is safe to dispose even when the logger ignores scopes.</returns>
    /// <example>
    /// <code>
    /// using var scope = CorrelationScope.Begin(logger, operationId: operation.Id, accountId: account.Id);
    /// logger.LogInformation("Operation started");
    /// </code>
    /// </example>
    public static IDisposable Begin(ILogger logger, string? operationId = null, string? itemId = null, string? syncJobId = null, string? accountId = null, string? remoteItemId = null)
    {
        var state = new List<KeyValuePair<string, object?>>(5);
        Add(state, OperationIdKey, operationId);
        Add(state, ItemIdKey, itemId);
        Add(state, SyncJobIdKey, syncJobId);
        Add(state, AccountIdKey, accountId);
        Add(state, RemoteItemIdKey, remoteItemId);

        return logger.BeginScope(state) ?? NoScope.Instance;
    }

    private static void Add(List<KeyValuePair<string, object?>> state, string key, string? value)
    {
        if (value is not null) state.Add(new KeyValuePair<string, object?>(key, value));
    }

    private sealed class NoScope : IDisposable
    {
        public static NoScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
