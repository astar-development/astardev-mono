namespace AStarDev.CloudSync.IPC;

/// <summary>The outcome of one health check. Carries no description, because a description may name an account.</summary>
/// <param name="Name">The name the check is registered under, such as <c>queue</c>.</param>
/// <param name="Status">How the check rates what it watches.</param>
public sealed record HealthCheckView(string Name, ProviderHealthState Status);
