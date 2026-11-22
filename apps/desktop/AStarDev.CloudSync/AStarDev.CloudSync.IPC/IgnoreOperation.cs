namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Drops a dead-lettered operation for good: it leaves the queue and is never run. Answered with <see cref="CommandOutcome.Accepted"/> once it is gone,
/// or <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is when the operation is not dead-lettered, for example because it was already retried or ignored.
/// </summary>
/// <param name="OperationId">The <see cref="DeadLetterView.OperationId"/> of the operation to drop.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new IgnoreOperation(deadLetter.OperationId), cancellationToken);
/// </code>
/// </example>
public sealed record IgnoreOperation(Guid OperationId) : ICommand;
