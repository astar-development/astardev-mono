using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The connected account and the token cache as it stands after sign-in.</summary>
internal sealed record SignInOutcome(ProviderAccount Account, byte[] Cache);
