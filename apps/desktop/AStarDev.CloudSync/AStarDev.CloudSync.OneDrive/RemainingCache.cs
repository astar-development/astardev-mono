namespace AStarDev.CloudSync.OneDrive;

/// <summary>A token cache after an account was removed.</summary>
/// <param name="Cache">The serialised cache.</param>
/// <param name="HasAccounts"><see langword="true"/> when another account is still signed in, so the cache must be kept.</param>
internal sealed record RemainingCache(byte[] Cache, bool HasAccounts);
