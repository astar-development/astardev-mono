namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class OptionShould
{
    [Fact]
    public void wrap_a_value_as_some() => Option.Some(42).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(42);

    [Fact]
    public void create_none() => Option.None<int>().Value.ShouldBeOfType<None<int>>();

    [Fact]
    public void convert_a_null_reference_to_none() => Option.FromNullable<string>(null).Value.ShouldBeOfType<None<string>>();

    [Fact]
    public void convert_a_reference_to_some() => Option.FromNullable<string>("a").Value.ShouldBeOfType<Some<string>>().Value.ShouldBe("a");

    [Fact]
    public void convert_a_value_type_without_a_value_to_none() => Option.FromNullable<int>(null).Value.ShouldBeOfType<None<int>>();

    [Fact]
    public void convert_a_value_type_with_a_value_to_some() => Option.FromNullable<int>(7).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(7);

    [Fact]
    public void match_the_some_handler_for_some() => Option.Some(3).Match(value => $"got {value}", () => "nothing").ShouldBe("got 3");

    [Fact]
    public void match_the_none_handler_for_none() => Option.None<int>().Match(value => $"got {value}", () => "nothing").ShouldBe("nothing");

    [Fact]
    public void map_a_present_value() => Option.Some("abc").Map(text => text.Length).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(3);

    [Fact]
    public void not_call_the_mapping_for_none()
    {
        var called = false;

        var mapped = Option.None<int>().Map(value =>
        {
            called = true;
            return value;
        });

        called.ShouldBeFalse();
        mapped.Value.ShouldBeOfType<None<int>>();
    }

    [Fact]
    public void bind_to_the_next_option_for_some() => Option.Some(2).Bind(value => Option.Some(value * 2)).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(4);

    [Fact]
    public void stay_none_when_binding_none() => Option.None<int>().Bind(value => Option.Some(value * 2)).Value.ShouldBeOfType<None<int>>();

    [Fact]
    public void return_the_fallback_for_none() => Option.None<int>().GetValueOrDefault(9).ShouldBe(9);

    [Fact]
    public void return_the_value_for_some() => Option.Some(1).GetValueOrDefault(9).ShouldBe(1);
}
