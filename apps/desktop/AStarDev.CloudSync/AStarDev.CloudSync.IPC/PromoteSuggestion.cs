namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Makes a suggested word an active user term, or brings back an ignored one. Safe to send twice.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="Word">The suggested word; it is normalised like any term text.</param>
/// <param name="CategoryId">The category to put the new user term in.</param>
/// <example>
/// <code>
/// await client.SendAsync(new PromoteSuggestion("zorblax", categoryId), cancellationToken);
/// </code>
/// </example>
public sealed record PromoteSuggestion(string Word, Guid CategoryId) : ICommand;
