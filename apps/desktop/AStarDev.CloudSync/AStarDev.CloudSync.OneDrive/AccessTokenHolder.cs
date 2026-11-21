namespace AStarDev.CloudSync.OneDrive;

/// <summary>Holds the access token from the latest sign-in so Graph requests can carry it. Register one instance for the process.</summary>
internal sealed class AccessTokenHolder
{
    private volatile string? current;

    /// <summary>The token to send, or <see langword="null"/> until a sign-in has succeeded.</summary>
    public string? Current => current;

    /// <summary>Replaces the held token. Requests already in flight keep the token they started with.</summary>
    public void Set(string token) => current = token;

    /// <summary>Forgets the held token, so later requests carry none. Safe to call when nothing is held.</summary>
    public void Clear() => current = null;
}
