using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class PhraseMatcherShould
{
    [Fact]
    public void prefer_the_longest_phrase_at_a_position() =>
        Matched("red car park", Term("red", Colour), Term("red car", Colour), Term("car park", Place)).ShouldBe(["red car"]);

    [Fact]
    public void match_each_token_to_at_most_one_phrase() =>
        Matched("red car park", Term("red car", Colour), Term("car park", Place)).ShouldBe(["red car"]);

    [Fact]
    public void match_singular_and_plural_forms_to_the_same_term() =>
        Matched("Cars", Term("car", Colour)).ShouldBe(["car"]);

    [Fact]
    public void return_every_term_that_shares_the_matched_phrase() =>
        Matched("china", Term("china", Colour), Term("china", Place)).ShouldBe(["china", "china"]);

    [Fact]
    public void never_match_a_term_whose_text_has_no_tokens() =>
        Matched("123", Term("123", Colour)).ShouldBeEmpty();

    [Fact]
    public void return_nothing_for_no_tokens() =>
        Matched("", Term("red", Colour)).ShouldBeEmpty();

    [Fact]
    public void return_nothing_when_there_are_no_terms() =>
        Matched("red", []).ShouldBeEmpty();

    private static string[] Matched(string text, params Domain.Term[] terms) =>
        [.. PhraseMatcher.Match(ClassificationTokeniser.Tokenise(text), PhraseIndex.Create(terms)).Select(term => term.Text)];
}
