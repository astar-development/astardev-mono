using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class FolderSegmentSignalShould
{
    private readonly FolderSegmentSignal signal = new();

    [Fact]
    public void return_a_folder_candidate_for_each_term_found_in_a_folder_name()
    {
        var candidates = signal.Evaluate(PathOf("Holidays/New York 2019/img001.jpg"), Vocabulary(Term("new york", Place), Term("holiday", Place)));

        Texts(candidates).ShouldBe(["holiday", "new york"]);
        candidates.ShouldAllBe(candidate => candidate.Signal == SignalKind.FolderName);
    }

    [Fact]
    public void ignore_the_file_name() =>
        signal.Evaluate(PathOf("Photos/red.jpg"), Vocabulary(Term("red", Colour))).ShouldBeEmpty();

    [Fact]
    public void match_regardless_of_the_folder_casing() =>
        signal.Evaluate(PathOf("RED/x.jpg"), Vocabulary(Term("red", Colour))).Count.ShouldBe(1);

    [Fact]
    public void return_nothing_when_the_path_has_no_folder() =>
        signal.Evaluate(PathOf("red.jpg"), Vocabulary(Term("red", Colour))).ShouldBeEmpty();
}
