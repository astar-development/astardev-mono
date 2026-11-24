namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Adds an active user term to a category. Safe to send twice.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="Text">The term text; it is normalised like any term text.</param>
/// <param name="CategoryId">The category to put the term in.</param>
/// <example>
/// <code>
/// await client.SendAsync(new CreateTerm("red", categoryId), cancellationToken);
/// </code>
/// </example>
public sealed record CreateTerm(string Text, Guid CategoryId) : ICommand;
