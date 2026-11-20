namespace AStarDev.CloudSync.Ports;

/// <summary>The account a provider connected, as the provider identifies it.</summary>
public sealed record ProviderAccount(string AccountIdentifier, string DisplayName);
