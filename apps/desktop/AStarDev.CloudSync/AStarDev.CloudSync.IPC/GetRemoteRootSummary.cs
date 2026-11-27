namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to list the root of the connected drive and report what it found.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot list the root, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account whose drive root is listed. Fails with <see cref="HandlerFailed"/> when the account is unknown.</param>
/// <example>
/// <code>
/// var summary = await client.QueryAsync(new GetRemoteRootSummary(account.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetRemoteRootSummary(Guid AccountId) : IQuery<RemoteRootSummaryView>;
