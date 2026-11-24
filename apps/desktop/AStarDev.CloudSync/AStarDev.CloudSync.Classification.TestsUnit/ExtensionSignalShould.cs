using AStarDev.CloudSync.Domain;
using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ExtensionSignalShould
{
    private readonly ExtensionSignal signal = new();

    [Theory]
    [InlineData("jpg")]
    [InlineData(".jpg")]
    public void return_a_high_extension_candidate_when_the_extension_matches_a_term(string termText) =>
        signal.Evaluate(PathOf("Photos/holiday.JPG"), Vocabulary(Term(termText, Colour))).Select(candidate => (candidate.Signal, candidate.Confidence)).ShouldBe([(SignalKind.Extension, ClassificationConfidence.High)]);

    [Fact]
    public void return_nothing_when_the_file_has_no_extension() =>
        signal.Evaluate(PathOf("Photos/holiday"), Vocabulary(Term("jpg", Colour))).ShouldBeEmpty();

    [Fact]
    public void return_nothing_when_only_a_folder_is_named_like_the_extension() =>
        signal.Evaluate(PathOf("jpg/holiday.png"), Vocabulary(Term("jpg", Colour))).ShouldBeEmpty();

    [Fact]
    public void return_nothing_when_the_term_category_is_unknown() =>
        signal.Evaluate(PathOf("a.jpg"), Vocabulary(Term("jpg", new CategoryId(Guid.NewGuid())))).ShouldBeEmpty();

    [Fact]
    public void return_a_low_candidate_when_the_term_is_only_suggested() =>
        signal.Evaluate(PathOf("a.jpg"), Vocabulary(Term("jpg", Colour, TermSource.Suggested, TermStatus.Active)))[0].Confidence.ShouldBe(ClassificationConfidence.Low);

    [Fact]
    public void return_nothing_for_an_ignored_term() =>
        signal.Evaluate(PathOf("a.jpg"), Vocabulary(Term("jpg", Colour, TermSource.Seed, TermStatus.Ignored))).ShouldBeEmpty();
}
