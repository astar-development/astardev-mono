namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon what waits in the queue and how many operations are in each status. Needs no parameters. Ask once after connecting, then follow <see cref="QueueChanged"/>.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the operations cannot be read.
/// </summary>
/// <example>
/// <code>
/// var queue = await client.QueryAsync(new GetQueue(), cancellationToken);
/// </code>
/// </example>
public sealed record GetQueue : IQuery<QueueView>;
