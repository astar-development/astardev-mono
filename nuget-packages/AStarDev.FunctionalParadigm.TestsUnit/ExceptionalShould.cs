namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class ExceptionalShould
{
    [Fact]
    public void create_a_success_from_the_factory() => Exceptional.Success(42).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(42);

    [Fact]
    public void create_a_failure_from_the_factory()
    {
        var boom = new InvalidOperationException("boom");

        Exceptional.Failure<int>(boom).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(boom);
    }

    [Fact]
    public void compare_successes_by_value() => Exceptional.Success(42).ShouldBe(Exceptional.Success(42));
}
