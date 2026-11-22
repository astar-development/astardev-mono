namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon what it is doing now. Needs no parameters. Ask once after connecting, so a UI opened mid-crawl shows progress at once, then follow <see cref="ActivityChanged"/>.
/// Fails with <see cref="HandlerFailed"/> only when the daemon cannot answer.
/// </summary>
/// <example>
/// <code>
/// var activities = await client.QueryAsync(new GetActivities(), cancellationToken);
/// </code>
/// </example>
public sealed record GetActivities : IQuery<ActivitiesView>;
