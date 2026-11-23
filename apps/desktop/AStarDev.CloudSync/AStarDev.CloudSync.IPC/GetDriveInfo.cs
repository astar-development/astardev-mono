namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon how full the connected drive is, as the provider reports it. Needs no parameters.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the provider cannot answer, for example before <see cref="ConnectAccount"/> succeeded.
/// </summary>
/// <example>
/// <code>
/// var drive = await client.QueryAsync(new GetDriveInfo(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDriveInfo : IQuery<DriveInfoView>;
