namespace AStarDev.CloudSync.IPC;

/// <summary>One classification term as the terms list shows it. The text comes from the user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Id">The term's identity.</param>
/// <param name="Text">The normalised term text.</param>
/// <param name="CategoryId">The category the term belongs to, as in <see cref="ClassificationCategoryView.Id"/>.</param>
/// <param name="Source">Where the term came from: <c>Seed</c>, <c>User</c>, <c>Auto</c> or <c>Suggested</c>.</param>
/// <param name="Ignored"><see langword="true"/> when the user ignored the term, so it classifies nothing.</param>
/// <param name="FileCount">How many files are linked to the term; zero for an ignored term.</param>
public sealed record ClassificationTermView(Guid Id, string Text, Guid CategoryId, string Source, bool Ignored, int FileCount)
{
    /// <summary>Returns a redacted form, because the text can come from a user's file names.</summary>
    public override string ToString() => "ClassificationTermView(redacted)";
}
