namespace AStarDev.CloudSync.OneDrive;

/// <summary>Holds the access token from the latest sign-in so Graph requests can carry it. Register one instance for the process.</summary>
internal sealed class AccessTokenHolder
{
    private volatile string? current;
    private long expiresUtcTicks;

    /// <summary>The token to send, or <see langword="null"/> until a sign-in has succeeded.</summary>
    public string? Current => current;

    /// <summary>Serialises renewals, so concurrent calls for one account renew its token once.</summary>
    public SemaphoreSlim RenewalGate { get; } = new(1, 1);

    /// <summary>Replaces the held token and records no expiry, so <see cref="IsDue"/> stays false. Requests already in flight keep the token they started with.</summary>
    public void Set(string token) => Set(token, default);

    /// <summary>Replaces the held token and records when it stops being accepted. A <see langword="default"/> <paramref name="expiresOn"/> means the expiry is not known.</summary>
    public void Set(string token, DateTimeOffset expiresOn)
    {
        Volatile.Write(ref expiresUtcTicks, expiresOn == default ? 0 : expiresOn.UtcTicks);
        current = token;
    }

    /// <summary>Forgets the held token and its expiry, so later requests carry none. Safe to call when nothing is held.</summary>
    public void Clear()
    {
        current = null;
        Volatile.Write(ref expiresUtcTicks, 0);
    }

    /// <summary>True when a token with a known expiry is held and <paramref name="margin"/> before that expiry has been reached at <paramref name="now"/>. False when nothing is held or the expiry is not known.</summary>
    public bool IsDue(DateTimeOffset now, TimeSpan margin) => current is not null && Volatile.Read(ref expiresUtcTicks) is var ticks and not 0 && ticks - margin.Ticks <= now.UtcTicks;
}
