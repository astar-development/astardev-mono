namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to recompute the automatic classification links of every file. The run goes on in the background: the daemon answers at once and then publishes <see cref="ReclassifyProgressChanged"/>.
/// Answered with <see cref="CommandOutcome.Rejected"/> and a message fit for display when a run is already active. User links and ignored terms are untouched.
/// </summary>
/// <example>
/// <code>
/// var started = await client.SendAsync(new StartReclassify(), cancellationToken);
/// </code>
/// </example>
public sealed record StartReclassify : ICommand;
