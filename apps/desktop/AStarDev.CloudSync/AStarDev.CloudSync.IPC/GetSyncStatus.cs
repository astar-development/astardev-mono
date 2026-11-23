namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the values of the status bar. Needs no parameters. Ask once after connecting, then follow <see cref="SyncStatusChanged"/>.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the operation queue cannot be read.
/// </summary>
/// <example>
/// <code>
/// var status = await client.QueryAsync(new GetSyncStatus(), cancellationToken);
/// </code>
/// </example>
public sealed record GetSyncStatus : IQuery<SyncStatusView>;
