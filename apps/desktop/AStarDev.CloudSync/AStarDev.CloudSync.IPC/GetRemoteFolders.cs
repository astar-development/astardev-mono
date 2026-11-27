namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the folders directly under one remote folder, so the UI can show a tree and load each level only when the user opens it.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot list the folder, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account whose drive is listed. Fails with <see cref="HandlerFailed"/> when the account is unknown.</param>
/// <param name="ParentId">Id of the folder to list, as an earlier <see cref="RemoteFolderView.Id"/> reported it. <see langword="null"/> lists the top of the drive.</param>
/// <example>
/// <code>
/// var top = await client.QueryAsync(new GetRemoteFolders(account.Id), cancellationToken);
/// var below = await client.QueryAsync(new GetRemoteFolders(account.Id, folder.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetRemoteFolders(Guid AccountId, string? ParentId = null) : IQuery<RemoteFoldersView>;
