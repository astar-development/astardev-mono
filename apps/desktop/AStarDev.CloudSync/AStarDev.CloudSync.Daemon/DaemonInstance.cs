using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Where the daemon's single-instance lock lives: beside its IPC socket, so a different socket path is a different daemon.</summary>
internal static class DaemonInstance
{
    /// <summary>Options for the lock and hand-off socket next to <paramref name="socketPath"/>, named after the socket file so they never collide with it.</summary>
    /// <param name="socketPath">The daemon's IPC socket path.</param>
    public static SingleInstanceOptions OptionsFor(string socketPath)
    {
        var fullPath = Path.GetFullPath(socketPath);
        var folder = Option.FromNullable(Path.GetDirectoryName(fullPath)).GetValueOrDefault(Path.GetTempPath());

        return SingleInstanceOptionsFactory.Create(folder, Path.GetFileName(fullPath) + ApplicationConstants.InstanceNameSuffix, null);
    }
}
