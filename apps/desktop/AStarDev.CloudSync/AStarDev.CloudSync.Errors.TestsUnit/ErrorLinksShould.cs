using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Errors.TestsUnit;

public sealed class ErrorLinksShould
{
    private static ErrorCode Code => ErrorCode.Create("CS-IPC-0003").Value.ShouldBeOfType<Ok<ErrorCode>>().Value;

    [Fact]
    public void build_the_link_under_the_documented_root()
    {
        ErrorLinks.Root.ShouldBe("https://astar-development.co.uk/errors/");
        ErrorLinks.For(Code).ShouldBe("https://astar-development.co.uk/errors/CS-IPC-0003");
    }

    [Fact]
    public void set_the_help_link_on_an_exception()
    {
        var exception = new InvalidOperationException("boom").WithHelpLink(Code);

        exception.HelpLink.ShouldBe("https://astar-development.co.uk/errors/CS-IPC-0003");
    }

    [Fact]
    public void return_the_same_exception_it_was_given()
    {
        var original = new InvalidOperationException("boom");

        original.WithHelpLink(Code).ShouldBeSameAs(original);
    }
}
