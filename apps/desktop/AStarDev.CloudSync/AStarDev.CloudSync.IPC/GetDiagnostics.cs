namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for its diagnostics: version, state, start time and the result of every registered health check. Needs no parameters.
/// The answer never carries a file path or an account name, so it is safe to show or copy as it is.
/// A failing health check is reported in the answer, not as a failure.
/// </summary>
/// <example>
/// <code>
/// var diagnostics = await client.QueryAsync(new GetDiagnostics(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDiagnostics : IQuery<DiagnosticsView>;
