namespace AStarDev.CloudSync.Ports;

/// <summary>Closed set of failures that cross the provider boundary. Provider-specific errors are translated into these.</summary>
public union ProviderError(RateLimited, Transient, AuthenticationFailed, Permanent);
