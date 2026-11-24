namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One term as text, ready to show as it is, with the values an edit starts from. The text comes from the user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Id">The term's identity, as the daemon names it.</param>
/// <param name="Header">The term text, saying so when the term is ignored.</param>
/// <param name="Detail">The category, where the term came from and how many files it has.</param>
/// <param name="Text">The term text alone, for the edit form.</param>
/// <param name="CategoryId">The category the term belongs to, for the edit form.</param>
/// <param name="Ignored"><see langword="true"/> when the user ignored the term, for the edit form.</param>
internal sealed record TermRowViewModel(Guid Id, string Header, string Detail, string Text, Guid CategoryId, bool Ignored)
{
    /// <summary>Returns a redacted form, because the text comes from the user's file names.</summary>
    public override string ToString() => "TermRowViewModel(redacted)";
}
