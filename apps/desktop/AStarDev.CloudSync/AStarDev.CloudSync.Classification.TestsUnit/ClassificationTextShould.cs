using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationTextShould
{
    [Theory]
    [InlineData("  Red   Car ", "red car")]
    [InlineData("JPG", "jpg")]
    [InlineData("café", "café")]
    public void trim_compose_lower_case_and_single_space_the_text(string text, string expected) =>
        ClassificationText.NormaliseTerm(text).Value.ShouldBeOfType<Some<string>>().Value.ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void return_none_when_nothing_is_left(string text) =>
        ClassificationText.NormaliseTerm(text).Value.ShouldBeOfType<None<string>>();
}
