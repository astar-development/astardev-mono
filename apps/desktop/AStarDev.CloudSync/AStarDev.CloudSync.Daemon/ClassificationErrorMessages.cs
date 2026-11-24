using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns classification store failures into text the UI can show to a person as it is.</summary>
internal static class ClassificationErrorMessages
{
    /// <summary>One sentence per <see cref="ClassificationError"/> case. <paramref name="failedAction"/> completes "The classification terms could not be ..." for a storage failure, such as <c>read</c>; <paramref name="subject"/> names what failed.</summary>
    /// <param name="error">The failure.</param>
    /// <param name="failedAction">The verb in the storage failure sentence.</param>
    /// <param name="subject">What the storage failure was about.</param>
    public static string Describe(ClassificationError error, string failedAction, string subject) => error switch
    {
        CategoryNotFound => "That category no longer exists.",
        CategoryAlreadyExists => "A category with that name already exists.",
        CategoryNotEmpty => "That category still holds terms, so it cannot be removed.",
        TermNotFound => "That term no longer exists.",
        TermAlreadyExists => "That category already holds that term.",
        ItemNotFound => "That file is no longer synchronised.",
        InvalidTermText => "A term needs some text.",
        StorageFailed e => $"The {subject} could not be {failedAction} because the database failed: {e.Message}"
    };
}
