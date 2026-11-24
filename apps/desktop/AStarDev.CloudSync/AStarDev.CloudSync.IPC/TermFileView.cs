namespace AStarDev.CloudSync.IPC;

/// <summary>One file linked to a term. The path comes from the user's files, so <see cref="ToString"/> is redacted.</summary>
/// <param name="ItemId">The file's identity.</param>
/// <param name="Path">Where the file sits below the sync root, with <c>/</c> separators.</param>
/// <param name="AccountName">The display name of the account that holds the file.</param>
public sealed record TermFileView(Guid ItemId, string Path, string AccountName)
{
    /// <summary>Returns a redacted form, because the path comes from the user's files.</summary>
    public override string ToString() => "TermFileView(redacted)";
}
