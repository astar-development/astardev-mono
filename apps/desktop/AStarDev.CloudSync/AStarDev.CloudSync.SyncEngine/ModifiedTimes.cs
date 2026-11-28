namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Compares file modified times with a tolerance, so a time stored before timestamps kept full ticks (0.1 ms) still matches the same time read again from disk or the provider.</summary>
internal static class ModifiedTimes
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>Whether <paramref name="first"/> and <paramref name="second"/> are less than 1 ms apart.</summary>
    public static bool Same(DateTimeOffset first, DateTimeOffset second) => (first - second).Duration() < Tolerance;
}
