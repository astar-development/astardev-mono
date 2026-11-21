namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class ExceptionalExtensionsShould
{
    private static readonly InvalidOperationException Boom = new("boom");

    private static Exceptional<int> Succeeded(int value = 42) => Exceptional.Success(value);

    private static Exceptional<int> Failed() => Exceptional.Failure<int>(Boom);

    private static Task<Exceptional<int>> SucceededTask(int value = 42) => Task.FromResult(Succeeded(value));

    private static Task<Exceptional<int>> FailedTask() => Task.FromResult(Failed());

    [Fact]
    public void match_a_success_to_the_success_handler()
        => Succeeded().Match(value => $"ok {value}", error => error.Message).ShouldBe("ok 42");

    [Fact]
    public void match_a_failure_to_the_failure_handler()
        => Failed().Match(value => $"ok {value}", error => error.Message).ShouldBe("boom");

    [Fact]
    public void get_the_value_of_a_success()
        => Succeeded().GetOrThrow().ShouldBe(42);

    [Fact]
    public void throw_the_captured_exception_of_a_failure()
        => Should.Throw<InvalidOperationException>(() => Failed().GetOrThrow()).Message.ShouldBe("boom");

    [Fact]
    public async Task match_a_success_asynchronously()
        => (await Succeeded().MatchAsync(value => Task.FromResult($"ok {value}"), error => error.Message)).ShouldBe("ok 42");

    [Fact]
    public async Task match_a_failure_asynchronously()
        => (await Failed().MatchAsync(value => Task.FromResult($"ok {value}"), error => error.Message)).ShouldBe("boom");

    [Fact]
    public void map_a_success_value()
        => Succeeded().Map(value => value * 2).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(84);

    [Fact]
    public void map_a_failure_without_calling_the_selector()
    {
        var called = false;

        var mapped = Failed().Map(value =>
        {
            called = true;
            return value;
        });

        mapped.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task map_a_success_with_an_async_selector()
        => (await Succeeded().MapAsync(value => Task.FromResult(value * 2))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(84);

    [Fact]
    public async Task map_a_failure_with_an_async_selector_without_calling_it()
    {
        var called = false;

        var mapped = await Failed().MapAsync(value =>
        {
            called = true;
            return Task.FromResult(value);
        });

        mapped.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task map_a_success_with_a_value_task_selector()
        => (await Succeeded().MapAsync(value => new ValueTask<int>(value * 2))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(84);

    [Fact]
    public async Task map_a_failure_with_a_value_task_selector()
        => (await Failed().MapAsync(value => new ValueTask<int>(value))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public void bind_a_success_to_the_next_result()
        => Succeeded().Bind(value => Exceptional.Success(value + 1)).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public void bind_a_success_to_a_failure()
        => Succeeded().Bind(_ => Failed()).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public void short_circuit_bind_on_a_failure()
    {
        var called = false;

        var bound = Failed().Bind(value =>
        {
            called = true;
            return Exceptional.Success(value);
        });

        bound.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task bind_a_success_asynchronously()
        => (await Succeeded().BindAsync(value => SucceededTask(value + 1))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public async Task short_circuit_async_bind_on_a_failure()
        => (await Failed().BindAsync(value => SucceededTask(value))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public void tap_a_success_and_return_it_unchanged()
    {
        var seen = 0;
        var original = Succeeded();

        var tapped = original.Tap(value => seen = value);

        seen.ShouldBe(42);
        tapped.ShouldBe(original);
    }

    [Fact]
    public void tap_a_failure_with_the_failure_handler()
    {
        Exception? seen = null;
        var original = Failed();

        var tapped = original.Tap(_ => throw new InvalidOperationException("must not run"), error => seen = error);

        seen.ShouldBeSameAs(Boom);
        tapped.ShouldBe(original);
    }

    [Fact]
    public void tap_a_failure_without_a_failure_handler()
    {
        var original = Failed();

        original.Tap(_ => throw new InvalidOperationException("must not run")).ShouldBe(original);
    }

    [Fact]
    public void tap_the_error_of_a_failure()
    {
        Exception? seen = null;
        var original = Failed();

        original.TapError(error => seen = error).ShouldBe(original);

        seen.ShouldBeSameAs(Boom);
    }

    [Fact]
    public void not_tap_the_error_of_a_success()
    {
        var called = false;
        var original = Succeeded();

        original.TapError(_ => called = true).ShouldBe(original);

        called.ShouldBeFalse();
    }

    [Fact]
    public void convert_a_success_to_an_ok_result()
        => Succeeded().ToResult(error => error.Message).Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);

    [Fact]
    public void convert_a_failure_to_a_failed_result_with_the_mapped_error()
        => Failed().ToResult(error => error.Message).Value.ShouldBeOfType<Fail<string>>().Error.ShouldBe("boom");

    [Fact]
    public async Task match_a_task_of_success()
        => (await SucceededTask().MatchAsync(value => $"ok {value}", error => error.Message)).ShouldBe("ok 42");

    [Fact]
    public async Task match_a_task_of_failure()
        => (await FailedTask().MatchAsync(value => $"ok {value}", error => error.Message)).ShouldBe("boom");

    [Fact]
    public async Task run_the_async_success_handler_for_a_task_of_success()
    {
        var seen = 0;
        var failed = false;

        await SucceededTask().MatchAsync(value =>
        {
            seen = value;
            return Task.CompletedTask;
        }, _ => failed = true);

        seen.ShouldBe(42);
        failed.ShouldBeFalse();
    }

    [Fact]
    public async Task run_the_failure_handler_for_a_task_of_failure()
    {
        Exception? seen = null;
        var succeeded = false;

        await FailedTask().MatchAsync(_ =>
        {
            succeeded = true;
            return Task.CompletedTask;
        }, error => seen = error);

        seen.ShouldBeSameAs(Boom);
        succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task map_a_task_of_success()
        => (await SucceededTask().MapAsync(value => value * 2)).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(84);

    [Fact]
    public async Task map_a_task_of_failure()
        => (await FailedTask().MapAsync(value => value * 2)).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public async Task map_a_task_of_success_with_an_async_selector()
        => (await SucceededTask().MapAsync(value => Task.FromResult(value * 2))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(84);

    [Fact]
    public async Task bind_a_task_of_success()
        => (await SucceededTask().BindAsync(value => SucceededTask(value + 1))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public async Task short_circuit_bind_on_a_task_of_failure()
        => (await FailedTask().BindAsync(value => SucceededTask(value))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public async Task bind_a_task_of_success_with_a_value_task_binder()
        => (await SucceededTask().BindAsync(value => new ValueTask<Exceptional<int>>(Exceptional.Success(value + 1)))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public async Task short_circuit_value_task_bind_on_a_task_of_failure()
        => (await FailedTask().BindAsync(value => new ValueTask<Exceptional<int>>(Exceptional.Success(value)))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public async Task tap_a_task_of_success()
    {
        var seen = 0;

        var tapped = await SucceededTask().TapAsync(value => seen = value);

        seen.ShouldBe(42);
        tapped.Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public async Task tap_a_task_of_failure()
    {
        Exception? seen = null;

        var tapped = await FailedTask().TapAsync(_ => throw new InvalidOperationException("must not run"), error => seen = error);

        seen.ShouldBeSameAs(Boom);
        tapped.Value.ShouldBeOfType<Failure<int>>();
    }

    [Fact]
    public async Task tap_the_error_of_a_task_of_failure()
    {
        Exception? seen = null;

        var tapped = await FailedTask().TapErrorAsync(error => seen = error);

        seen.ShouldBeSameAs(Boom);
        tapped.Value.ShouldBeOfType<Failure<int>>();
    }

    [Fact]
    public async Task not_tap_the_error_of_a_task_of_success()
    {
        var called = false;

        await SucceededTask().TapErrorAsync(_ => called = true);

        called.ShouldBeFalse();
    }

    [Fact]
    public async Task ensure_runs_after_a_task_of_success_and_keeps_the_result()
    {
        var ran = false;

        var ensured = await SucceededTask().EnsureAsync(() => ran = true);

        ran.ShouldBeTrue();
        ensured.Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public async Task ensure_runs_after_a_task_of_failure_and_keeps_the_result()
    {
        var ran = false;

        var ensured = await FailedTask().EnsureAsync(() => ran = true);

        ran.ShouldBeTrue();
        ensured.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);
    }

    [Fact]
    public async Task convert_a_task_of_success_to_an_ok_result()
        => (await SucceededTask().ToResultAsync(error => error.Message)).Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);

    [Fact]
    public async Task convert_a_task_of_failure_to_a_failed_result()
        => (await FailedTask().ToResultAsync(error => error.Message)).Value.ShouldBeOfType<Fail<string>>().Error.ShouldBe("boom");

    [Fact]
    public async Task bind_a_value_task_of_success_with_a_task_binder()
        => (await new ValueTask<Exceptional<int>>(Succeeded()).BindAsync(value => SucceededTask(value + 1))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public async Task short_circuit_task_bind_on_a_value_task_of_failure()
        => (await new ValueTask<Exceptional<int>>(Failed()).BindAsync(value => SucceededTask(value))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public async Task bind_a_value_task_of_success_with_a_value_task_binder()
        => (await new ValueTask<Exceptional<int>>(Succeeded()).BindAsync(value => new ValueTask<Exceptional<int>>(Exceptional.Success(value + 1)))).Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(43);

    [Fact]
    public async Task short_circuit_value_task_bind_on_a_value_task_of_failure()
        => (await new ValueTask<Exceptional<int>>(Failed()).BindAsync(value => new ValueTask<Exceptional<int>>(Exceptional.Success(value)))).Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(Boom);

    [Fact]
    public async Task tap_a_value_task_of_success()
    {
        var seen = 0;

        var tapped = await new ValueTask<Exceptional<int>>(Succeeded()).TapAsync(value => seen = value);

        seen.ShouldBe(42);
        tapped.Value.ShouldBeOfType<Success<int>>();
    }

    [Fact]
    public async Task tap_a_value_task_of_failure()
    {
        Exception? seen = null;

        var tapped = await new ValueTask<Exceptional<int>>(Failed()).TapAsync(_ => throw new InvalidOperationException("must not run"), error => seen = error);

        seen.ShouldBeSameAs(Boom);
        tapped.Value.ShouldBeOfType<Failure<int>>();
    }
}
