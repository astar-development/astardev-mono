using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Suggests terms from the words in file paths.</summary>
/// <param name="files">Lists the stored files.</param>
/// <param name="terms">Stores terms.</param>
/// <param name="knownWords">The common-word list.</param>
/// <param name="firstNames">The first-name list.</param>
/// <param name="vocabulary">Told to reload after a term changed.</param>
internal sealed class ClassificationSuggestions(IClassifiableFileSource files, ITermStore terms, IKnownWordList knownWords, IFirstNameList firstNames, IClassificationVocabularyProvider vocabulary) : IClassificationSuggestions, IDisposable
{
    private const int PageSize = 5000;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => writeGate.Dispose();

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ClassificationSuggestion>, ClassificationError>> GetSuggestionsAsync(CancellationToken cancellationToken)
    {
        var stored = await terms.ListAsync(cancellationToken).ConfigureAwait(false);
        if (stored.Value is not Ok<IReadOnlyList<Term>> storedTerms) return new Fail<ClassificationError>(new StorageFailed("The terms could not be read"));

        var knownTokens = storedTerms.Value.Select(term => ClassificationTokeniser.Tokenise(term.Text)).Where(tokens => tokens.Count == 1).Select(tokens => tokens[0]).ToHashSet(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        ItemId? after = null;
        while (true)
        {
            var page = await files.GetPageAsync(after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.Value is not Ok<IReadOnlyList<ClassifiableFile>> loaded) return new Fail<ClassificationError>(new StorageFailed("The files could not be read"));

            foreach (var word in loaded.Value.SelectMany(file => SuggestionWords.Split(file.Path.Value)).Where(word => !IsKnown(word, knownTokens)))
                counts[word] = counts.GetValueOrDefault(word) + 1;

            if (loaded.Value.Count < PageSize) break;

            after = loaded.Value[^1].ItemId;
        }

        return new Ok<IReadOnlyList<ClassificationSuggestion>>(Rank(counts));
    }

    /// <inheritdoc />
    public Task<Result<Term, ClassificationError>> PromoteAsync(string word, CategoryId categoryId, CancellationToken cancellationToken) => WriteAsync(word, categoryId, Promote, cancellationToken);

    /// <inheritdoc />
    public Task<Result<Term, ClassificationError>> IgnoreAsync(string word, CategoryId categoryId, CancellationToken cancellationToken) => WriteAsync(word, categoryId, Ignore, cancellationToken);

    private static Term? Promote(Term? existing, Term candidate) => existing switch
    {
        { Status: TermStatus.Active, Source: not TermSource.Suggested } => null,
        { } found => found with { Source = TermSource.User, Status = TermStatus.Active },
        _ => candidate with { Source = TermSource.User, Status = TermStatus.Active }
    };

    private static Term? Ignore(Term? existing, Term candidate) => existing switch
    {
        { Status: TermStatus.Ignored } => null,
        { } found => found with { Status = TermStatus.Ignored },
        _ => candidate with { Source = TermSource.Suggested, Status = TermStatus.Ignored }
    };

    private bool IsKnown(string word, HashSet<string> knownTokens) => knownTokens.Contains(ClassificationTokeniser.Tokenise(word)[0]) || knownWords.Contains(word);

    private IReadOnlyList<ClassificationSuggestion> Rank(Dictionary<string, int> counts) =>
        [.. counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new ClassificationSuggestion(pair.Key, pair.Value, firstNames.Contains(pair.Key) ? SuggestionHint.FirstName : SuggestionHint.Plain))];

    private async Task<Result<Term, ClassificationError>> WriteAsync(string word, CategoryId categoryId, Func<Term?, Term, Term?> decide, CancellationToken cancellationToken)
    {
        if (ClassificationText.NormaliseTerm(word) is not Some<string> text) return new Fail<ClassificationError>(new InvalidTermText(word));

        var candidate = new Term(new TermId(Guid.NewGuid()), text.Value, categoryId, TermSource.Suggested, TermStatus.Active);
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SaveAsync(candidate, decide, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task<Result<Term, ClassificationError>> SaveAsync(Term candidate, Func<Term?, Term, Term?> decide, CancellationToken cancellationToken)
    {
        var stored = await terms.ListAsync(cancellationToken).ConfigureAwait(false);
        if (stored.Value is not Ok<IReadOnlyList<Term>> all) return new Fail<ClassificationError>(new StorageFailed("The terms could not be read"));

        var existing = all.Value.FirstOrDefault(term => term.CategoryId == candidate.CategoryId && term.Text == candidate.Text);
        if (decide(existing, candidate) is not { } changed) return new Ok<Term>(existing ?? candidate);

        var written = existing is null ? await terms.AddAsync(changed, cancellationToken).ConfigureAwait(false) : await terms.UpdateAsync(changed, cancellationToken).ConfigureAwait(false);
        vocabulary.Invalidate();

        return written.Value switch
        {
            Fail<ClassificationError> failure => failure,
            _ => new Ok<Term>(changed)
        };
    }
}
