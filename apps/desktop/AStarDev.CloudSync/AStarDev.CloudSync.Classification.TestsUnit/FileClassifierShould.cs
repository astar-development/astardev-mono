using static AStarDev.CloudSync.Classification.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class FileClassifierShould
{
    private readonly FileClassifier classifier = new([new ExtensionSignal(), new FolderSegmentSignal(), new DictionaryTermSignal()]);

    [Fact]
    public void link_a_file_to_terms_from_extension_folder_and_words() =>
        Texts(classifier.Classify(PathOf("Holidays/Red_Car.jpg"), Vocabulary(Term("jpg", Colour), Term("holiday", Place), Term("red", Colour))).Candidates).Order().ShouldBe(["holiday", "jpg", "red"]);

    [Fact]
    public void return_one_candidate_when_two_signals_find_the_same_term() =>
        classifier.Classify(PathOf("red/red.jpg"), Vocabulary(Term("red", Colour))).Candidates.Count.ShouldBe(1);

    [Fact]
    public void return_unclassified_for_an_empty_vocabulary() =>
        classifier.Classify(PathOf("Holidays/Red_Car.jpg"), Vocabulary()).Candidates.ShouldBeEmpty();

    [Fact]
    public void return_the_path_it_classified() =>
        classifier.Classify(PathOf("a/b.jpg"), Vocabulary()).Path.ShouldBe(PathOf("a/b.jpg"));
}
