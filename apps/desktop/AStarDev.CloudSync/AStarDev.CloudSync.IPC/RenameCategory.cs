namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Gives a classification category a new name. Its terms stay.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="CategoryId">The category, as in <see cref="ClassificationCategoryView.Id"/>.</param>
/// <param name="Name">The new name. Trimmed; it must differ from every other category name, ignoring case.</param>
/// <example>
/// <code>
/// await client.SendAsync(new RenameCategory(categoryId, "Colours"), cancellationToken);
/// </code>
/// </example>
public sealed record RenameCategory(Guid CategoryId, string Name) : ICommand;
