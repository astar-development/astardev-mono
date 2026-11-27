namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for one page of the operations that gave up after their retries and wait for a decision. Ask once after connecting and again after <see cref="RetryOperation"/> or <see cref="IgnoreOperation"/>.
/// Fails with <see cref="HandlerFailed"/> only when the daemon cannot read its queue.
/// </summary>
/// <param name="Skip">How many operations to pass over, in claim order (priority, then age). Zero or more; a negative value counts as zero.</param>
/// <param name="Take">The most operations to return. The daemon returns between 1 and 500.</param>
/// <example>
/// <code>
/// var deadLetters = await client.QueryAsync(new GetDeadLetters(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDeadLetters(int Skip = 0, int Take = 200) : IQuery<DeadLettersView>;
