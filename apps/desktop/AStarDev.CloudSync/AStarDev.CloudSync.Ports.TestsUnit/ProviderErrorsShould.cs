namespace AStarDev.CloudSync.Ports.TestsUnit;

public sealed class ProviderErrorsShould
{
    [Fact]
    public void be_a_closed_set_of_four_cases()
    {
        ProviderError[] errors =
        [
            new RateLimited(TimeSpan.FromSeconds(5)),
            new Transient("timeout"),
            new AuthenticationFailed("token expired"),
            new Permanent("access denied")
        ];

        errors.Select(DescribeExhaustively).ShouldBe(["rate limited", "transient", "authentication failed", "permanent"]);
    }

    [Fact]
    public void carry_retry_after_on_rate_limited()
    {
        ProviderError error = new RateLimited(TimeSpan.FromSeconds(30));

        error.Value.ShouldBeOfType<RateLimited>().RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    private static string DescribeExhaustively(ProviderError error) => error switch
    {
        RateLimited => "rate limited",
        Transient => "transient",
        AuthenticationFailed => "authentication failed",
        Permanent => "permanent"
    };
}
