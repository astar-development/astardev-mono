namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Removes a classification category that holds no terms.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="CategoryId">The category, as in <see cref="ClassificationCategoryView.Id"/>. It must hold no terms.</param>
/// <example>
/// <code>
/// await client.SendAsync(new DeleteCategory(categoryId), cancellationToken);
/// </code>
/// </example>
public sealed record DeleteCategory(Guid CategoryId) : ICommand;
