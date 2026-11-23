using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The scope has no completed crawl or no delta token to continue from, so there is nothing to ask the provider about.</summary>
/// <param name="Scope">The scope that was processed.</param>
internal sealed record ScopeNotReady(SyncScope Scope) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => SyncEngineErrorCodes.ScopeNotReady;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The provider could not return the changes.</summary>
/// <param name="Error">The provider's reason; <see cref="InvalidDeltaToken"/> means the token is spent and the scope must be listed again.</param>
internal sealed record DeltaProviderFailed(ProviderError Error) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => SyncEngineErrorCodes.DeltaProviderFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The stored state could not be read or written. The old delta token is still stored.</summary>
/// <param name="Error">The store's reason.</param>
internal sealed record DeltaStoreFailed(StoreError Error) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => SyncEngineErrorCodes.DeltaStoreFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The token was invalid and listing the scope again did not complete, or gave no new token. The scope's crawl checkpoint is kept, so the next run continues the listing.</summary>
/// <param name="Scope">The scope that was reindexed.</param>
internal sealed record ReindexFailed(SyncScope Scope) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => SyncEngineErrorCodes.ReindexFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Closed set of reasons delta processing of a scope stopped.</summary>
internal union DeltaError(ScopeNotReady, DeltaProviderFailed, DeltaStoreFailed, ReindexFailed);
