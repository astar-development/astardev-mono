using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>What the UI knows about a kind of provider: its name and capabilities. A new provider needs only a descriptor, not a new screen.</summary>
/// <param name="ProviderType">The identifier the daemon reports for accounts of this provider.</param>
/// <param name="DisplayName">The name shown on cards and in the add journey.</param>
/// <param name="IsAvailable">False for a placeholder that is listed but cannot be connected yet.</param>
/// <param name="Capabilities">The features that drive which actions a card offers. Empty means only the actions every provider has.</param>
internal sealed record ProviderDescriptor(string ProviderType, string DisplayName, bool IsAvailable, IReadOnlyList<ProviderCapability> Capabilities);
