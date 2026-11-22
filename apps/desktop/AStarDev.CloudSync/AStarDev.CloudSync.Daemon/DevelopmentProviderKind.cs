#if DEBUG
namespace AStarDev.CloudSync.Daemon;

/// <summary>Which cloud provider a debug build of the daemon talks to.</summary>
internal enum DevelopmentProviderKind
{
    /// <summary>The real OneDrive provider. The default.</summary>
    OneDrive,

    /// <summary>The in-memory fake provider with a generated tree.</summary>
    Fake
}
#endif
