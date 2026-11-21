namespace AStarDev.CloudSync.IPC;

/// <summary>Whether the daemon can act for an account without asking the user.</summary>
public enum AccountState
{
    /// <summary>The stored sign-in works.</summary>
    Connected,

    /// <summary>The provider refused the stored sign-in. Send <see cref="ConnectAccount"/> to sign in again.</summary>
    ReauthenticationRequired
}
