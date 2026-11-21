namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for every connected account and whether each one needs the user to sign in again. Needs no parameters.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the accounts cannot be read.
/// </summary>
/// <example>
/// <code>
/// var accounts = await client.QueryAsync(new GetAccounts(), cancellationToken);
/// </code>
/// </example>
public sealed record GetAccounts : IQuery<AccountsView>;
