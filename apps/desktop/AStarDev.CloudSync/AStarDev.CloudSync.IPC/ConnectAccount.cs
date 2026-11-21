namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to sign in to the cloud provider. The daemon opens the system browser, so the answer can take as long as the user takes to sign in.
/// Answered with <see cref="CommandOutcome.Accepted"/> once signed in, or <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is
/// (sign-in refused, keyring unavailable, provider unreachable). On success the daemon also publishes <see cref="AccountConnected"/>.
/// </summary>
/// <example>
/// <code>
/// var result = await client.SendAsync(new ConnectAccount(), cancellationToken);
/// </code>
/// </example>
public sealed record ConnectAccount : ICommand;
