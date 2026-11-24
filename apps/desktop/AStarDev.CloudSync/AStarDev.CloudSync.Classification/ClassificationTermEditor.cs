using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Edits terms through the term store.</summary>
/// <param name="terms">Stores terms.</param>
internal sealed class ClassificationTermEditor(ITermStore terms) : IClassificationTermEditor
{
    /// <inheritdoc />
    public async Task<Result<Term, ClassificationError>> UpdateAsync(TermId id, string text, CategoryId categoryId, bool ignored, CancellationToken cancellationToken)
    {
        if (ClassificationText.NormaliseTerm(text) is not Some<string> normalised) return new Fail<ClassificationError>(new InvalidTermText(text));

        var stored = await terms.ListAsync(cancellationToken).ConfigureAwait(false);
        if (stored.Value is not Ok<IReadOnlyList<Term>> all) return new Fail<ClassificationError>(new StorageFailed("The terms could not be read"));

        var existing = all.Value.FirstOrDefault(term => term.Id == id);
        if (existing is null) return new Fail<ClassificationError>(new TermNotFound(id));

        var edited = existing with { Text = normalised.Value, CategoryId = categoryId, Status = ignored ? TermStatus.Ignored : TermStatus.Active, Source = existing.Text == normalised.Value && existing.CategoryId == categoryId ? existing.Source : TermSource.User };
        var written = await terms.UpdateAsync(edited, cancellationToken).ConfigureAwait(false);

        return written.Value switch
        {
            Fail<ClassificationError> failure => failure,
            _ => new Ok<Term>(edited)
        };
    }
}
