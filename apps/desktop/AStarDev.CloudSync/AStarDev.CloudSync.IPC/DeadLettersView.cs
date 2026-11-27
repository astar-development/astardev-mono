namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetDeadLetters"/>.</summary>
/// <param name="Total">How many operations have given up in all, so a caller can ask for the next page.</param>
/// <param name="DeadLetters">This page of the dead-lettered operations, oldest first; empty when no operation has given up or the page is past the end.</param>
public sealed record DeadLettersView(int Total, IReadOnlyList<DeadLetterView> DeadLetters);
