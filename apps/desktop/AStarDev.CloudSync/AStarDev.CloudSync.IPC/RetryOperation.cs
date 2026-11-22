namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Puts a dead-lettered operation back in the queue with its attempts reset, so the daemon tries it again. Answered with <see cref="CommandOutcome.Accepted"/> once it is queued,
/// or <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is when the operation is not dead-lettered, for example because it was already retried or ignored.
/// </summary>
/// <param name="OperationId">The <see cref="DeadLetterView.OperationId"/> of the operation to retry.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new RetryOperation(deadLetter.OperationId), cancellationToken);
/// </code>
/// </example>
public sealed record RetryOperation(Guid OperationId) : ICommand;
