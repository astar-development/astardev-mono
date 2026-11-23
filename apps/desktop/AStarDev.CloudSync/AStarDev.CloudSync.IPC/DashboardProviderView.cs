namespace AStarDev.CloudSync.IPC;

/// <summary>One provider type of the dashboard with the worst health among its accounts.</summary>
/// <param name="Name">The provider type, for example <c>OneDrive</c>.</param>
/// <param name="Health">Degraded when any of the provider's accounts must sign in again, otherwise healthy.</param>
public sealed record DashboardProviderView(string Name, ProviderHealthState Health);
