namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to list the root of the connected drive and report what it found. Needs no parameters.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot list the root, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <example>
/// <code>
/// var summary = await client.QueryAsync(new GetRemoteRootSummary(), cancellationToken);
/// </code>
/// </example>
public sealed record GetRemoteRootSummary : IQuery<RemoteRootSummaryView>;
