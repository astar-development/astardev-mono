using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Converts between the bootstrap mode the UI sends and the one the Domain stores.</summary>
internal static class BootstrapModes
{
    /// <summary>Maps the IPC mode onto the Domain mode. A value outside the enum falls back to <see cref="BootstrapMode.Merge"/>, the mode that discards nothing.</summary>
    public static BootstrapMode ToDomain(SyncBootstrapMode mode) => mode switch
    {
        SyncBootstrapMode.Download => BootstrapMode.Download,
        SyncBootstrapMode.Upload => BootstrapMode.Upload,
        _ => BootstrapMode.Merge
    };

    /// <summary>Maps the Domain mode onto the IPC mode. A value outside the enum falls back to <see cref="SyncBootstrapMode.Merge"/>.</summary>
    public static SyncBootstrapMode ToView(BootstrapMode mode) => mode switch
    {
        BootstrapMode.Download => SyncBootstrapMode.Download,
        BootstrapMode.Upload => SyncBootstrapMode.Upload,
        _ => SyncBootstrapMode.Merge
    };
}
