namespace AStarDev.CloudSync.Ports;

public sealed record RateLimited(TimeSpan RetryAfter);
