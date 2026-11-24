using AStarDev.CloudSync.Domain;
using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationCombinerShould
{
    private static readonly Category ColourCategory = new(Colour, "Colour");

    [Fact]
    public void prefer_high_confidence_for_the_same_term()
    {
        var low = Candidate(Term("china", Colour), ClassificationConfidence.Low);
        var high = Candidate(Term("china", Colour), ClassificationConfidence.High);

        ClassificationCombiner.Combine(PathOf("a/b.jpg"), [low, high]).Candidates.ShouldBe([high]);
    }

    [Fact]
    public void keep_one_candidate_when_the_same_text_and_category_come_from_different_term_ids() =>
        ClassificationCombiner.Combine(PathOf("a/b.jpg"), [Candidate(Term("red", Colour), ClassificationConfidence.High), Candidate(Term("red", Colour), ClassificationConfidence.High)]).Candidates.Count.ShouldBe(1);

    [Fact]
    public void keep_both_candidates_when_the_same_text_is_in_two_categories() =>
        ClassificationCombiner.Combine(PathOf("a/b.jpg"), [Candidate(Term("china", Colour), ClassificationConfidence.High), Candidate(Term("china", Place), ClassificationConfidence.High)]).Candidates.Count.ShouldBe(2);

    [Fact]
    public void drop_the_candidate_of_an_ignored_term() =>
        ClassificationCombiner.Combine(PathOf("a/b.jpg"), [Candidate(Term("red", Colour, TermSource.Seed, TermStatus.Ignored), ClassificationConfidence.High)]).Candidates.ShouldBeEmpty();

    [Fact]
    public void return_unclassified_when_there_are_no_candidates() =>
        ClassificationCombiner.Combine(PathOf("a/b.jpg"), []).Candidates.ShouldBeEmpty();

    private static ClassificationCandidate Candidate(Term term, ClassificationConfidence confidence) => new(term, ColourCategory, SignalKind.Vocabulary, confidence);
}
