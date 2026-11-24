namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One file linked to the selected term, as text. The path comes from the user's files, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Path">Where the file sits below the sync root.</param>
/// <param name="Account">The name of the account that holds the file.</param>
internal sealed record FileRowViewModel(string Path, string Account)
{
    /// <summary>Returns a redacted form, because the path comes from the user's files.</summary>
    public override string ToString() => "FileRowViewModel(redacted)";
}
