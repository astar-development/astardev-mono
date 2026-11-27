namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon how full the connected drive is, as the provider reports it. 
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot answer, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account whose drive is measured. Fails with <see cref="HandlerFailed"/> when the account is unknown.</param>
/// <example>
/// <code>
/// var drive = await client.QueryAsync(new GetDriveInfo(account.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetDriveInfo(Guid AccountId) : IQuery<DriveInfoView>;
