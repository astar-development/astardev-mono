using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The scope has no completed crawl or no delta token to continue from, so there is nothing to ask the provider about.</summary>
/// <param name="Scope">The scope that was processed.</param>
internal sealed record ScopeNotReady(SyncScope Scope);

/// <summary>The provider could not return the changes.</summary>
/// <param name="Error">The provider's reason; <see cref="InvalidDeltaToken"/> means the token is spent and the scope must be listed again.</param>
internal sealed record DeltaProviderFailed(ProviderError Error);

/// <summary>The stored state could not be read or written. The old delta token is still stored.</summary>
/// <param name="Error">The store's reason.</param>
internal sealed record DeltaStoreFailed(StoreError Error);

/// <summary>Closed set of reasons delta processing of a scope stopped.</summary>
internal union DeltaError(ScopeNotReady, DeltaProviderFailed, DeltaStoreFailed);
