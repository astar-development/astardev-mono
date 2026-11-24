namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Links a term to a file as the user's own choice. Automatic classification never removes it. Safe to send twice.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="ItemId">The file, as in <see cref="TermFileView.ItemId"/>.</param>
/// <param name="TermId">The term, as in <see cref="ClassificationTermView.Id"/>.</param>
/// <example>
/// <code>
/// await client.SendAsync(new AssignTerm(itemId, termId), cancellationToken);
/// </code>
/// </example>
public sealed record AssignTerm(Guid ItemId, Guid TermId) : ICommand;
