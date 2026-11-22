namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetDeadLetters"/>.</summary>
/// <param name="DeadLetters">The dead-lettered operations, oldest first; empty when no operation has given up.</param>
public sealed record DeadLettersView(IReadOnlyList<DeadLetterView> DeadLetters);
