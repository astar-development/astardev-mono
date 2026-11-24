using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

internal sealed class PhraseIndex
{
    private PhraseIndex(Dictionary<string, Term[]> phrases, int longestPhrase)
    {
        Phrases = phrases.GetAlternateLookup<ReadOnlySpan<char>>();
        LongestPhrase = longestPhrase;
    }

    internal Dictionary<string, Term[]>.AlternateLookup<ReadOnlySpan<char>> Phrases { get; }

    internal int LongestPhrase { get; }

    internal static PhraseIndex Create(IReadOnlyList<Term> terms)
    {
        var tokenised = terms.Select(term => (Term: term, Phrase: ClassificationTokeniser.Tokenise(term.Text))).ToList();
        var phrases = tokenised
            .GroupBy(entry => string.Join(' ', entry.Phrase), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Term).ToArray(), StringComparer.Ordinal);

        return new(phrases, tokenised.Select(entry => entry.Phrase.Count).DefaultIfEmpty(0).Max());
    }
}
