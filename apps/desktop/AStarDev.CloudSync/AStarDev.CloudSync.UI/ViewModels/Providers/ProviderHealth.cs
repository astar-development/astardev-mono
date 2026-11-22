namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>How one connected provider account is doing. Independent of the overall sync health on the dashboard, and shown as colour, icon and text together.</summary>
internal enum ProviderHealth
{
    /// <summary>The account is connected and nothing needs attention.</summary>
    Healthy,

    /// <summary>The account is exchanging changes right now.</summary>
    Syncing,

    /// <summary>The account works but the user should look at it soon.</summary>
    Warning,

    /// <summary>The account has failed and sync for it has stopped.</summary>
    Error,

    /// <summary>The provider refused the stored sign-in; the user must sign in again.</summary>
    AuthenticationRequired,

    /// <summary>The provider cannot be reached.</summary>
    Offline
}
