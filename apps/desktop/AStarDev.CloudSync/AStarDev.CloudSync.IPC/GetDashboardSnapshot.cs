namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for every value the dashboard shows. Needs no parameters. Ask once after connecting, then follow <see cref="DashboardChanged"/>.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when a store the snapshot is read from cannot be read.
/// </summary>
/// <example>
/// <code>
/// var snapshot = await client.QueryAsync(new GetDashboardSnapshot(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDashboardSnapshot : IQuery<DashboardView>;
