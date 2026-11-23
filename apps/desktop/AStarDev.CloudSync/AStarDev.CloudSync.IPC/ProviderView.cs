namespace AStarDev.CloudSync.IPC;

/// <summary>One provider the daemon can connect accounts to.</summary>
/// <param name="ProviderType">The identifier accounts of this provider carry in <see cref="AccountView.ProviderType"/>, such as <c>OneDrive</c>.</param>
/// <param name="Capabilities">What the registered provider implementation can do; empty when it offers only what every provider does.</param>
public sealed record ProviderView(string ProviderType, IReadOnlyList<ProviderCapability> Capabilities);
