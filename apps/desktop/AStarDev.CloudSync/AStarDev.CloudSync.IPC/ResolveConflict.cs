namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Applies one decision to one or many open conflicts, for the UI's single and bulk resolve actions. The daemon queues the work, journals every decision and publishes <see cref="ConflictResolved"/> for each resolved conflict;
/// <see cref="ConflictChoice.Defer"/> publishes nothing and leaves the conflict listed. Answered with <see cref="CommandOutcome.Accepted"/> when every conflict was handled,
/// <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is when any was not (the others are still resolved), or <see cref="CommandOutcome.ValidationError"/> when no id is given.
/// </summary>
/// <param name="ConflictIds">The <see cref="ConflictView.ConflictId"/> values to resolve; one for a single resolve. Must not be empty.</param>
/// <param name="Choice">The one decision applied to every listed conflict.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new ResolveConflict([conflict.ConflictId], ConflictChoice.KeepBoth), cancellationToken);
/// </code>
/// </example>
public sealed record ResolveConflict(IReadOnlyList<Guid> ConflictIds, ConflictChoice Choice) : ICommand;
