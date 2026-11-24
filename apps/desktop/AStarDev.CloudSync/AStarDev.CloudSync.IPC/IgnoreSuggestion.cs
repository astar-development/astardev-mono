namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Marks a suggested word ignored so it is never suggested again. The term is kept, never deleted. Safe to send twice.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="Word">The suggested word; it is normalised like any term text.</param>
/// <param name="CategoryId">The category used when the term has to be created.</param>
/// <example>
/// <code>
/// await client.SendAsync(new IgnoreSuggestion("zorblax", categoryId), cancellationToken);
/// </code>
/// </example>
public sealed record IgnoreSuggestion(string Word, Guid CategoryId) : ICommand;
