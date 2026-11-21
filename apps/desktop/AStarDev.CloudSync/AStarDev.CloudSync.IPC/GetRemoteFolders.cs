namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the folders directly under one remote folder, so the UI can show a tree and load each level only when the user opens it.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot list the folder, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <param name="ParentId">Id of the folder to list, as an earlier <see cref="RemoteFolderView.Id"/> reported it. <see langword="null"/> lists the top of the drive.</param>
/// <example>
/// <code>
/// var top = await client.QueryAsync(new GetRemoteFolders(), cancellationToken);
/// var below = await client.QueryAsync(new GetRemoteFolders(folder.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetRemoteFolders(string? ParentId = null) : IQuery<RemoteFoldersView>;
