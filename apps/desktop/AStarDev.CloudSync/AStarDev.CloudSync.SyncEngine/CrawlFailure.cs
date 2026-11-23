using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.SyncEngine;

internal sealed record CrawlFailure(string Reason, ErrorCode Code);
