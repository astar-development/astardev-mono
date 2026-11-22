namespace AStarDev.CloudSync.SyncEngine;

/// <summary>What one run of the initial crawl achieved.</summary>
/// <param name="ScopesCompleted">Scopes now fully indexed, including those finished by an earlier run.</param>
/// <param name="ScopesFailed">Scopes that stopped on an error and keep their checkpoint, so the next run continues them.</param>
internal sealed record CrawlSummary(int ScopesCompleted, int ScopesFailed);
