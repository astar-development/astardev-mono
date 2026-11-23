namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for every user setting. Needs no parameters. Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the settings cannot be read.
/// </summary>
/// <example>
/// <code>
/// var settings = await client.QueryAsync(new GetSettings(), cancellationToken);
/// </code>
/// </example>
public sealed record GetSettings : IQuery<SettingsView>;
