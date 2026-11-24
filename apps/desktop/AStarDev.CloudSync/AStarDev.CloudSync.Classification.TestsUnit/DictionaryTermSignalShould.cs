using AStarDev.CloudSync.Domain;
using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class DictionaryTermSignalShould
{
    private readonly DictionaryTermSignal signal = new();

    [Fact]
    public void return_the_longest_phrase_found_in_the_file_name()
    {
        var candidates = signal.Evaluate(PathOf("Photos/Red_Car_2020.jpg"), Vocabulary(Term("red", Colour), Term("red car", Place)));

        Texts(candidates).ShouldBe(["red car"]);
        candidates[0].Signal.ShouldBe(SignalKind.Vocabulary);
    }

    [Fact]
    public void match_the_words_of_a_camel_case_file_name() =>
        Texts(signal.Evaluate(PathOf("RedCar.png"), Vocabulary(Term("car", Place)))).ShouldBe(["car"]);

    [Fact]
    public void ignore_the_extension_and_the_folders() =>
        signal.Evaluate(PathOf("red/photo.car"), Vocabulary(Term("red", Colour), Term("car", Place))).ShouldBeEmpty();

    [Fact]
    public void return_a_low_candidate_when_the_term_is_only_suggested() =>
        signal.Evaluate(PathOf("red.jpg"), Vocabulary(Term("red", Colour, TermSource.Suggested, TermStatus.Active)))[0].Confidence.ShouldBe(ClassificationConfidence.Low);

    [Fact]
    public void return_nothing_for_an_empty_vocabulary() =>
        signal.Evaluate(PathOf("red.jpg"), Vocabulary()).ShouldBeEmpty();
}
