namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon which providers it supports and, for every connected account, its last sync, storage and operation counts. Needs no parameters.
/// Storage is the provider's quota and comes from one call to the provider, so it can be slow; when that call fails the storage figures are <see langword="null"/> rather than the query failing.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, only when a store cannot be read.
/// </summary>
/// <example>
/// <code>
/// var providers = await client.QueryAsync(new GetProviders(), cancellationToken);
/// </code>
/// </example>
public sealed record GetProviders : IQuery<ProvidersView>;
