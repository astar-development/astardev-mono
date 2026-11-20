namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class MapErrorShould
{
    [Fact]
    public void leave_a_success_untouched()
    {
        Result<int, string> result = new Ok<int>(42);

        var mapped = result.MapError(error => error.Length);

        mapped.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public void convert_the_error_of_a_failure()
    {
        Result<int, string> result = new Fail<string>("boom");

        var mapped = result.MapError(error => error.Length);

        mapped.Value.ShouldBeOfType<Fail<int>>().Error.ShouldBe(4);
    }

    [Fact]
    public void not_call_the_mapping_for_a_success()
    {
        Result<int, string> result = new Ok<int>(42);
        var called = false;

        _ = result.MapError(error =>
        {
            called = true;
            return error;
        });

        called.ShouldBeFalse();
    }
}
