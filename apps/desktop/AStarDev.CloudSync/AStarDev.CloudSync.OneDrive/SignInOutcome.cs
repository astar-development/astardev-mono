using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The connected account, the token cache as it stands after sign-in, and the access token that sign-in returned, with when it expires. A <see langword="default"/> <c>ExpiresOn</c> means the expiry is not known.</summary>
internal sealed record SignInOutcome(ProviderAccount Account, byte[] Cache, string AccessToken, DateTimeOffset ExpiresOn = default);
