#if DEBUG
namespace AStarDev.CloudSync.Daemon;

/// <summary>The <c>CloudSync</c> configuration section of a debug build: development-only switches that never exist in a release build.</summary>
internal sealed class DevelopmentProviderOptions
{
    /// <summary>Which provider the daemon uses. <see cref="DevelopmentProviderKind.OneDrive"/> when unset.</summary>
    public DevelopmentProviderKind Provider { get; set; } = DevelopmentProviderKind.OneDrive;

    /// <summary>How many items the fake provider's generated tree holds. Only read when <see cref="Provider"/> is <see cref="DevelopmentProviderKind.Fake"/>.</summary>
    public int FakeTreeSize { get; set; } = DefaultFakeTreeSize;

    /// <summary>The tree size the manual crash-recovery checks use.</summary>
    public const int DefaultFakeTreeSize = 700_000;
}
#endif
