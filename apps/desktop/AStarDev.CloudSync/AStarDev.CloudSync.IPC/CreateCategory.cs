namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Adds a classification category.
/// Answered with a <see cref="CommandResult"/>: <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.Rejected"/> with a message fit for display when the daemon cannot do it, or <see cref="CommandOutcome.ValidationError"/> when the text given is blank.
/// </summary>
/// <param name="Name">The new name. Trimmed; it must differ from every other category name, ignoring case.</param>
/// <example>
/// <code>
/// await client.SendAsync(new CreateCategory("Colour"), cancellationToken);
/// </code>
/// </example>
public sealed record CreateCategory(string Name) : ICommand;
