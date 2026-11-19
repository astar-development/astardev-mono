namespace AStarDev.CloudSync.Domain.TestsUnit;

public sealed class ProviderExceptionsShould
{
    [Fact]
    public void share_a_common_base_type()
    {
        var exceptions = new ProviderException[]
        {
            new RateLimitException("throttled"),
            new TransientProviderException("timeout"),
            new AuthenticationFailureException("expired"),
            new PermanentProviderException("denied", new InvalidOperationException("root cause"))
        };

        exceptions.ShouldAllBe(e => e is Exception);
        exceptions.Select(e => e.GetType()).Distinct().Count().ShouldBe(4);
        exceptions[3].InnerException.ShouldBeOfType<InvalidOperationException>();
    }
}
