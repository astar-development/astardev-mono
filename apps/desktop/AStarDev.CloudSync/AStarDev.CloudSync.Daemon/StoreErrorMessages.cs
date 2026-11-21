using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns account store failures into text the UI can show to a person as it is.</summary>
internal static class StoreErrorMessages
{
    /// <summary>One sentence per <see cref="StoreError"/> case. <paramref name="failedAction"/> completes "The account could not be ..." for a storage failure, such as <c>saved</c> or <c>removed</c>.</summary>
    public static string Describe(StoreError error, string failedAction) => error switch
    {
        AccountNotFound => "That account is no longer connected.",
        AccountAlreadyExists => "That account is already connected.",
        StorageFailed e => $"The account could not be {failedAction} because the database failed: {e.Message}"
    };
}
