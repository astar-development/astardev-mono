namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Replaces the text, category and ignored state of a term. Its links stay.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="TermId">The term, as in <see cref="ClassificationTermView.Id"/>.</param>
/// <param name="Text">The new text; it is normalised like any term text.</param>
/// <param name="CategoryId">The category the term now belongs to.</param>
/// <param name="Ignored"><see langword="true"/> to ignore the term so it classifies nothing.</param>
/// <example>
/// <code>
/// await client.SendAsync(new UpdateTerm(termId, "crimson", categoryId, false), cancellationToken);
/// </code>
/// </example>
public sealed record UpdateTerm(Guid TermId, string Text, Guid CategoryId, bool Ignored) : ICommand;
