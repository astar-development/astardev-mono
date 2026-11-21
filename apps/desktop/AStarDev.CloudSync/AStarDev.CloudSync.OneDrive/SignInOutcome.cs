using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The connected account, the token cache as it stands after sign-in, and the access token that sign-in returned.</summary>
internal sealed record SignInOutcome(ProviderAccount Account, byte[] Cache, string AccessToken);
