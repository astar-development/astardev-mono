namespace AStarDev.CloudSync.Domain;

/// <summary>Whether the daemon can currently act for an <see cref="Account"/>.</summary>
public enum AccountStatus
{
    /// <summary>The provider accepted the stored sign-in, so the account can be used without asking the user.</summary>
    Connected,

    /// <summary>The provider refused the stored sign-in. The account is paused until the user signs in again; nothing is deleted.</summary>
    ReauthenticationRequired
}
