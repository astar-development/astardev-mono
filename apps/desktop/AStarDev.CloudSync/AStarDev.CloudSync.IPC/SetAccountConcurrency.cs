namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to change how many of one account's uploads, downloads, deletes and moves run at once. Answered with <see cref="CommandOutcome.Accepted"/> once stored; the next claim honours it and
/// operations already running finish. <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is when the value is not one of <see cref="AllowedValues"/> or the account is unknown.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account to change.</param>
/// <param name="MaxConcurrentOperations">The new limit, one of <see cref="AllowedValues"/>.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new SetAccountConcurrency(account.Id, 6), cancellationToken);
/// </code>
/// </example>
public sealed record SetAccountConcurrency(Guid AccountId, int MaxConcurrentOperations) : ICommand
{
    /// <summary>The only limits the daemon accepts, smallest first.</summary>
    public static IReadOnlyList<int> AllowedValues { get; } = [1, 2, 4, 6, 8];
}
