namespace AStarDev.CloudSync.UI;

/// <summary>Stops a cancelled command from ending the process: a toolkit command cancels its earlier run when it executes again, and rethrows that cancellation on the UI thread.</summary>
internal static class CancellationGuard
{
    /// <summary>True when the UI thread may drop the exception: a cancellation is not a failure. Any other exception stays unhandled.</summary>
    /// <param name="exception">The unhandled exception raised on the UI thread.</param>
    public static bool Swallows(Exception exception) => exception is OperationCanceledException;
}
