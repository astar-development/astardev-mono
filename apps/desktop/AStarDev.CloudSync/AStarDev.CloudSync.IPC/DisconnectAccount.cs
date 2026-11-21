namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to forget an account and its stored sign-in. Answered with <see cref="CommandOutcome.Accepted"/> once both are gone, or
/// <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is (unknown account, keyring unavailable). On success the daemon also publishes <see cref="AccountDisconnected"/>.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account to disconnect.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new DisconnectAccount(account.Id), cancellationToken);
/// </code>
/// </example>
public sealed record DisconnectAccount(Guid AccountId) : ICommand;
