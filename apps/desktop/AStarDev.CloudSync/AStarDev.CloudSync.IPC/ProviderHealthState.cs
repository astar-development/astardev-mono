namespace AStarDev.CloudSync.IPC;

/// <summary>How the daemon's health checks rate the cloud provider, from best to worst.</summary>
public enum ProviderHealthState
{
    /// <summary>Every check passed, or there is no account the provider could be failing for.</summary>
    Healthy,

    /// <summary>The provider works but needs the user, for example an account must sign in again.</summary>
    Degraded,

    /// <summary>The daemon could not read the state it needs to judge the provider.</summary>
    Unhealthy
}
