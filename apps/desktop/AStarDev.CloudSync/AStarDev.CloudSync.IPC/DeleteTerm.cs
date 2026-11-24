namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Removes a term and every link to it.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="TermId">The term, as in <see cref="ClassificationTermView.Id"/>. Its links go with it.</param>
/// <example>
/// <code>
/// await client.SendAsync(new DeleteTerm(termId), cancellationToken);
/// </code>
/// </example>
public sealed record DeleteTerm(Guid TermId) : ICommand;
