namespace AStarDev.CloudSync.IPC;

/// <summary>Asks the daemon whether a reclassify run is active and how far it has got. Needs no parameters. A UI that opens mid-run asks once, then follows <see cref="ReclassifyProgressChanged"/>.</summary>
/// <example>
/// <code>
/// var status = await client.QueryAsync(new GetReclassifyStatus(), cancellationToken);
/// </code>
/// </example>
public sealed record GetReclassifyStatus : IQuery<ReclassifyStatusView>;
