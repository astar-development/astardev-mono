using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

internal static class PhraseMatcher
{
    private const int StackTokenCount = 64;

    internal static IReadOnlyList<Term> Match(IReadOnlyList<string> tokens, PhraseIndex index)
    {
        var matches = new List<Term>();
        MatchInto(matches, tokens, index);

        return matches;
    }

    internal static void MatchInto(List<Term> matches, IReadOnlyList<string> tokens, PhraseIndex index)
    {
        if (tokens.Count == 0 || index.LongestPhrase == 0) return;

        var text = string.Join(' ', tokens);
        var starts = tokens.Count < StackTokenCount ? stackalloc int[StackTokenCount] : new int[tokens.Count + 1];
        for (var token = 0; token < tokens.Count; token++)
            starts[token + 1] = starts[token] + tokens[token].Length + 1;

        var position = 0;
        while (position < tokens.Count)
            position += MatchAt(matches, text, starts, position, Math.Min(index.LongestPhrase, tokens.Count - position), index);
    }

    private static int MatchAt(List<Term> matches, string text, ReadOnlySpan<int> starts, int position, int longest, PhraseIndex index)
    {
        for (var size = longest; size > 0; size--)
        {
            if (!index.Phrases.TryGetValue(text.AsSpan(starts[position], starts[position + size] - starts[position] - 1), out var terms)) continue;

            matches.AddRange(terms);

            return size;
        }

        return 1;
    }
}
