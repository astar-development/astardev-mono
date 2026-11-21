namespace AStarDev.FunctionalParadigm.TestsUnit;

public sealed class TryShould
{
    [Fact]
    public void return_the_value_when_the_operation_succeeds()
    {
        var result = Try.Run(() => 42);

        result.Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public void capture_a_thrown_exception_as_a_failure()
    {
        var boom = new InvalidOperationException("boom");

        var result = Try.Run<int>(() => throw boom);

        result.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(boom);
    }

    [Fact]
    public void rethrow_operation_cancelled() =>
        Should.Throw<OperationCanceledException>(() => Try.Run<int>(() => throw new OperationCanceledException()));

    [Fact]
    public async Task return_the_value_when_the_async_operation_succeeds()
    {
        var result = await Try.RunAsync(_ => Task.FromResult(42), TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Success<int>>().Value.ShouldBe(42);
    }

    [Fact]
    public async Task capture_an_exception_thrown_by_an_async_operation_as_a_failure()
    {
        var boom = new InvalidOperationException("boom");

        var result = await Try.RunAsync<int>(_ => Task.FromException<int>(boom), TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Failure<int>>().Exception.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task pass_the_token_to_the_async_operation()
    {
        using var source = new CancellationTokenSource();

        var result = await Try.RunAsync(token => Task.FromResult(token), source.Token);

        result.Value.ShouldBeOfType<Success<CancellationToken>>().Value.ShouldBe(source.Token);
    }

    [Fact]
    public async Task rethrow_operation_cancelled_from_an_async_operation()
    {
        using var source = new CancellationTokenSource();

        await Should.ThrowAsync<OperationCanceledException>(Try.RunAsync(async token =>
        {
            await source.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);

            return 0;
        }, source.Token));
    }

    [Fact]
    public async Task not_start_when_already_cancelled()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var started = false;

        await Should.ThrowAsync<OperationCanceledException>(Try.RunAsync(_ =>
        {
            started = true;
            
            return Task.FromResult(0);
        }, source.Token));

        started.ShouldBeFalse();
    }
}
