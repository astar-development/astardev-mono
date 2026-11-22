namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon which operations gave up after their retries and wait for a decision. Needs no parameters. Ask once after connecting and again after <see cref="RetryOperation"/> or <see cref="IgnoreOperation"/>.
/// Fails with <see cref="HandlerFailed"/> only when the daemon cannot read its queue.
/// </summary>
/// <example>
/// <code>
/// var deadLetters = await client.QueryAsync(new GetDeadLetters(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDeadLetters : IQuery<DeadLettersView>;
