namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class ResultShould
{
    [Fact]
    public void hold_a_success_value()
    {
        Result<int, string> result = new Ok<int>(42);

        result.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public void hold_a_failure_error()
    {
        Result<int, string> result = new Fail<string>("boom");

        result.Value.ShouldBeOfType<Fail<string>>().Error.ShouldBe("boom");
    }

    [Fact]
    public void hold_a_success_that_carries_no_value()
    {
        Result<Unit, string> result = new Ok<Unit>(default);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public void match_exhaustively_on_both_cases()
    {
        Result<int, string> success = new Ok<int>(42);
        Result<int, string> failure = new Fail<string>("boom");

        Describe(success).ShouldBe("ok 42");
        Describe(failure).ShouldBe("fail boom");
    }

    // No discard arm: the build fails (CS8509) if the union stops being exhaustive.
    private static string Describe(Result<int, string> result) => result switch
    {
        Ok<int> ok => $"ok {ok.Value}",
        Fail<string> fail => $"fail {fail.Error}"
    };
}
